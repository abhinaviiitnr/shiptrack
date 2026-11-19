using Orders.Api.Application.DTOs;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;

namespace Orders.Api.Application.Commands;

// WHY: Creating an order involves: validate -> fetch prices -> reserve stock ->
// compute shipping -> save. If save fails we must release stock (compensation).
// All this logic is in one handler class — not in the controller.
public sealed class CreateOrderHandler(
    IInventoryClient inventory,
    IOrderRepository orders,
    ShippingStrategySelector shippingSelector,
    ILogger<CreateOrderHandler> logger)
{
    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderRequest req, Guid customerId, CancellationToken ct)
    {
        // 1. Fetch product prices from Inventory (uses resilient HttpClient).
        IReadOnlyList<ProductPriceDto> products;
        try
        {
            var ids = req.Items.Select(i => i.ProductId);
            products = await inventory.GetProductPricesAsync(ids, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch product prices from Inventory.");
            return CreateOrderResult.InventoryUnavailable("Inventory service is unavailable.");
        }

        // 2. Validate all requested products exist.
        foreach (var item in req.Items)
        {
            if (!products.Any(p => p.Id == item.ProductId))
                return CreateOrderResult.ProductNotFound(item.ProductId);
        }

        // 3. Reserve stock — ALL OR NOTHING.
        var reserveItems = req.Items
            .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
            .ToList();

        InventoryReserveResult reserveResult;
        try
        {
            reserveResult = await inventory.ReserveAsync(reserveItems, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to call reserve on Inventory.");
            return CreateOrderResult.InventoryUnavailable("Could not reserve stock.");
        }

        if (!reserveResult.Success)
            return CreateOrderResult.StockInsufficient(reserveResult.ErrorDetail ?? "Insufficient stock.");

        // 4. Compute shipping via Strategy pattern.
        var shippingMethod = (ShippingMethod)(int)req.ShippingMethod;
        var strategy = shippingSelector.GetStrategy(shippingMethod);
        var itemsTotal = req.Items.Sum(i =>
        {
            var price = products.First(p => p.Id == i.ProductId).Price;
            return price * i.Quantity;
        });
        var shippingCost = strategy.Calculate(itemsTotal);

        // 5. Build the order entity.
        var order = new Order
        {
            CustomerId = customerId,
            ShippingMethod = shippingMethod,
            ShippingCost = shippingCost,
            TotalAmount = itemsTotal + shippingCost,
            Items = req.Items.Select(i =>
            {
                var p = products.First(prod => prod.Id == i.ProductId);
                return new OrderItem
                {
                    OrderId = Guid.Empty,  // Will be set by EF
                    ProductId = i.ProductId,
                    ProductNameSnapshot = p.Name,
                    UnitPriceSnapshot = p.Price,
                    Quantity = i.Quantity
                };
            }).ToList(),
            StatusHistory =
            [
                new OrderStatusHistory
                {
                    FromStatus = OrderStatus.Confirmed,
                    ToStatus = OrderStatus.Confirmed,
                    ChangedBy = customerId.ToString()
                }
            ]
        };

        // 6. Save. If this fails, compensate by releasing the reserved stock.
        try
        {
            await orders.AddAsync(order, ct);
            await orders.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save order. Releasing reserved stock.");
            try { await inventory.ReleaseAsync(reserveItems, CancellationToken.None); }
            catch (Exception releaseEx)
            {
                // WHY: Log separately — we cannot crash here even if release fails.
                // A monitoring alert on this log message should trigger a manual fix.
                logger.LogCritical(releaseEx,
                    "COMPENSATION FAILED: could not release stock after order save failure. " +
                    "Items: {Items}", reserveItems);
            }
            throw;  // Re-throw so the global handler returns 500.
        }

        return CreateOrderResult.Ok(MapToDto(order));
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(), o.ShippingMethod.ToString(),
        o.ShippingCost, o.TotalAmount, o.CreatedAt, o.UpdatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductNameSnapshot,
            i.UnitPriceSnapshot, i.Quantity)).ToList(),
        o.StatusHistory.Select(h => new StatusHistoryDto(
            h.FromStatus.ToString(), h.ToStatus.ToString(), h.ChangedAt, h.ChangedBy)).ToList());
}

// Result type avoids exception-driven control flow for expected business outcomes.
public sealed record CreateOrderResult(bool IsSuccess, OrderDto? Order, string? ErrorCode, string? Detail, Guid? ProductId)
{
    public static CreateOrderResult Ok(OrderDto order) => new(true, order, null, null, null);
    public static CreateOrderResult StockInsufficient(string detail) =>
        new(false, null, "INSUFFICIENT_STOCK", detail, null);
    public static CreateOrderResult ProductNotFound(Guid id) =>
        new(false, null, "PRODUCT_NOT_FOUND", $"Product {id} not found.", id);
    public static CreateOrderResult InventoryUnavailable(string detail) =>
        new(false, null, "INVENTORY_UNAVAILABLE", detail, null);
}
