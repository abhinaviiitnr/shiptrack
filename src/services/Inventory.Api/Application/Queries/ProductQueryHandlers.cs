using Inventory.Api.Application.DTOs;
using Inventory.Api.Domain;
using Inventory.Api.Infrastructure;

namespace Inventory.Api.Application.Queries;

public sealed class GetProductByIdQuery(IProductRepository repo)
{
    public async Task<ProductDto?> HandleAsync(Guid id, CancellationToken ct)
    {
        var product = await repo.FindByIdAsync(id, ct);
        return product?.ToDto();
    }
}

public sealed class GetProductsQuery(IProductRepository repo)
{
    public async Task<PagedResult<ProductDto>> HandleAsync(
        string? name, int page, int pageSize, CancellationToken ct)
    {
        var (items, total) = await repo.SearchAsync(name, page, pageSize, ct);
        return new PagedResult<ProductDto>(
            items.Select(p => p.ToDto()).ToList(),
            page, pageSize, total);
    }
}

public sealed class GetProductBatchQuery(IProductRepository repo)
{
    public async Task<IReadOnlyList<ProductDto>> HandleAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var products = await repo.FindByIdsAsync(ids, ct);
        return products.Select(p => p.ToDto()).ToList();
    }
}
