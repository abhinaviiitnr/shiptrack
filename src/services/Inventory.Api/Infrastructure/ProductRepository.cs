using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

public sealed class ProductRepository(InventoryDbContext db) : IProductRepository
{
    public Task<Product?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<IReadOnlyList<Product>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => db.Products
             .Where(p => ids.Contains(p.Id))
             .ToListAsync(ct)
             .ContinueWith(t => (IReadOnlyList<Product>)t.Result, ct);

    public async Task<(IReadOnlyList<Product> Items, int Total)> SearchAsync(
        string? name, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{name}%"));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await db.Products.AddAsync(product, ct);

    public Task DeleteAsync(Product product, CancellationToken ct = default)
    {
        db.Products.Remove(product);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
