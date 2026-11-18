using Orders.Api.Domain;

namespace Orders.Api.Infrastructure;

public interface IOrderRepository
{
    Task<Order?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<OrderSummaryDto> Items, Guid? NextCursor)> GetAdminOrdersAsync(
        AdminOrderFilter filter, CancellationToken ct = default);
    Task<(IReadOnlyList<OrderSummaryDto> Items, Guid? NextCursor)> GetCustomerOrdersAsync(
        Guid customerId, Guid? cursor, int pageSize, CancellationToken ct = default);
}

public sealed record AdminOrderFilter(
    OrderStatus? Status,
    DateTime? From,
    DateTime? To,
    Guid? CustomerId,
    Guid? Cursor,
    int PageSize = 20);

// A flat DTO for list views — avoids loading Items and StatusHistory unnecessarily.
public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    string ShippingMethod,
    decimal TotalAmount,
    DateTime CreatedAt);
