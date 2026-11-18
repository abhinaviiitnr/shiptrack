using Inventory.Api.Domain;

namespace Inventory.Api.Infrastructure;

public interface IProductRepository
{
    Task<Product?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int Total)> SearchAsync(string? name, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task DeleteAsync(Product product, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
