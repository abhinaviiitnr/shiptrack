using Orders.Api.Application.DTOs;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;

namespace Orders.Api.Application.Commands;

public sealed class UpdateOrderStatusHandler(
    IOrderRepository orders)
{
    public async Task<UpdateStatusResult> HandleAsync(
        Guid orderId, string targetStatusStr, string changedBy, CancellationToken ct)
    {
        var order = await orders.FindByIdAsync(orderId, ct);
        if (order is null) return UpdateStatusResult.NotFound();

        if (!Enum.TryParse<OrderStatus>(targetStatusStr, ignoreCase: true, out var targetStatus))
            return UpdateStatusResult.InvalidTransition($"'{targetStatusStr}' is not a valid status.");

        if (!OrderStateMachine.CanTransition(order.Status, targetStatus))
            return UpdateStatusResult.InvalidTransition(
                $"Cannot transition from {order.Status} to {targetStatus}. " +
                $"Valid next statuses: {string.Join(", ", OrderStateMachine.GetValidNextStatuses(order.Status))}");

        order.StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = targetStatus,
            ChangedBy = changedBy
        });
        order.Status = targetStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await orders.SaveChangesAsync(ct);
        return UpdateStatusResult.Ok();
    }
}

public sealed class CancelOrderHandler(
    IOrderRepository orders,
    IInventoryClient inventory,
    ILogger<CancelOrderHandler> logger)
{
    public async Task<CancelOrderResult> HandleAsync(
        Guid orderId, Guid requestingUserId, bool isAdmin, CancellationToken ct)
    {
        var order = await orders.FindByIdAsync(orderId, ct);
        if (order is null) return CancelOrderResult.NotFound();

        // WHY: Only the order owner or an admin can cancel.
        if (!isAdmin && order.CustomerId != requestingUserId)
            return CancelOrderResult.Forbidden();

        if (!OrderStateMachine.CanTransition(order.Status, OrderStatus.Cancelled))
            return CancelOrderResult.InvalidTransition(
                $"Cannot cancel an order in '{order.Status}' status.");

        // Release stock back to inventory (best-effort compensation).
        var releaseItems = order.Items
            .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
            .ToList();

        try
        {
            await inventory.ReleaseAsync(releaseItems, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to release stock for cancelled order {OrderId}.", orderId);
            // WHY: Don't block the cancellation even if release fails.
            // A monitoring alert should trigger a manual inventory audit.
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = OrderStatus.Cancelled,
            ChangedBy = requestingUserId.ToString()
        });
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        await orders.SaveChangesAsync(ct);
        return CancelOrderResult.Ok();
    }
}

public sealed record UpdateStatusResult(bool IsSuccess, bool IsNotFound, string? ErrorDetail)
{
    public static UpdateStatusResult Ok() => new(true, false, null);
    public static UpdateStatusResult NotFound() => new(false, true, null);
    public static UpdateStatusResult InvalidTransition(string detail) => new(false, false, detail);
}

public sealed record CancelOrderResult(bool IsSuccess, bool IsNotFound, bool IsForbidden, string? ErrorDetail)
{
    public static CancelOrderResult Ok() => new(true, false, false, null);
    public static CancelOrderResult NotFound() => new(false, true, false, null);
    public static CancelOrderResult Forbidden() => new(false, false, true, null);
    public static CancelOrderResult InvalidTransition(string detail) => new(false, false, false, detail);
}
