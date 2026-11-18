namespace Orders.Api.Application.DTOs;

public sealed record CreateOrderRequest(
    IReadOnlyList<OrderItemRequest> Items,
    ShippingMethodDto ShippingMethod);

public sealed record OrderItemRequest(Guid ProductId, int Quantity);

public enum ShippingMethodDto { Standard = 1, Express = 2 }

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    string ShippingMethod,
    decimal ShippingCost,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<StatusHistoryDto> StatusHistory);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);

public sealed record StatusHistoryDto(
    string FromStatus,
    string ToStatus,
    DateTime ChangedAt,
    string ChangedBy);

public sealed record UpdateStatusRequest(string Status);

public sealed record CancelOrderRequest();

// DTOs for communicating with Inventory service.
public sealed record ProductPriceDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);

public sealed record ReserveItemDto(Guid ProductId, int Quantity);

public sealed record PagedCursorResult<T>(
    IReadOnlyList<T> Items,
    Guid? NextCursor);
