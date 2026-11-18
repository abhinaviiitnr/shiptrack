namespace Inventory.Api.Application.DTOs;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity);

public sealed record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

// Internal endpoints DTOs
public sealed record ReserveItem(Guid ProductId, int Quantity);
public sealed record ReserveRequest(IReadOnlyList<ReserveItem> Items);

public sealed record ReleaseItem(Guid ProductId, int Quantity);
public sealed record ReleaseRequest(IReadOnlyList<ReleaseItem> Items);
