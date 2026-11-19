using Orders.Api.Application.DTOs;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;

namespace Orders.Api.Application.Queries;

public sealed class GetOrderByIdQuery(IOrderRepository orders)
{
    public async Task<OrderDto?> HandleAsync(Guid id, Guid requestingUserId, bool isAdmin, CancellationToken ct)
    {
        var order = await orders.FindByIdAsync(id, ct);
        if (order is null) return null;

        // Access control: customer can only read their own order
        if (!isAdmin && order.CustomerId != requestingUserId)
            return null; // Return null -> 404 to avoid leaking existence of other orders

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.ShippingMethod.ToString(),
            order.ShippingCost,
            order.TotalAmount,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductNameSnapshot, i.UnitPriceSnapshot, i.Quantity)).ToList(),
            order.StatusHistory.Select(h => new StatusHistoryDto(h.FromStatus.ToString(), h.ToStatus.ToString(), h.ChangedAt, h.ChangedBy)).ToList()
        );
    }
}

public sealed class GetCustomerOrdersQuery(IOrderRepository orders)
{
    public async Task<PagedCursorResult<OrderSummaryDto>> HandleAsync(
        Guid customerId, Guid? cursor, int pageSize, CancellationToken ct)
    {
        if (pageSize is < 1 or > 100) pageSize = 20;
        var (items, nextCursor) = await orders.GetCustomerOrdersAsync(customerId, cursor, pageSize, ct);
        return new PagedCursorResult<OrderSummaryDto>(items, nextCursor);
    }
}

public sealed class GetAdminOrdersQuery(IOrderRepository orders)
{
    public async Task<PagedCursorResult<OrderSummaryDto>> HandleAsync(
        AdminOrderFilter filter, CancellationToken ct)
    {
        filter = filter with { PageSize = Math.Clamp(filter.PageSize, 1, 100) };
        var (items, nextCursor) = await orders.GetAdminOrdersAsync(filter, ct);
        return new PagedCursorResult<OrderSummaryDto>(items, nextCursor);
    }
}
