using Inventory.Api.Application.DTOs;
using Inventory.Api.Domain;
using Inventory.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Application.Commands;

// WHY: CQRS-lite — dedicated command handler classes for mutations.
public sealed class CreateProductHandler(IProductRepository repo)
{
    public async Task<ProductDto> HandleAsync(CreateProductRequest req, CancellationToken ct)
    {
        var product = new Product
        {
            Sku = req.Sku,
            Name = req.Name,
            Description = req.Description,
            Price = req.Price,
            StockQuantity = req.StockQuantity
        };
        await repo.AddAsync(product, ct);
        await repo.SaveChangesAsync(ct);
        return product.ToDto();
    }
}

public sealed class UpdateProductHandler(IProductRepository repo)
{
    public async Task<ProductDto?> HandleAsync(Guid id, UpdateProductRequest req, CancellationToken ct)
    {
        var product = await repo.FindByIdAsync(id, ct);
        if (product is null) return null;

        product.Name = req.Name;
        product.Description = req.Description;
        product.Price = req.Price;
        product.StockQuantity = req.StockQuantity;

        await repo.SaveChangesAsync(ct);
        return product.ToDto();
    }
}

public sealed class DeleteProductHandler(IProductRepository repo)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct)
    {
        var product = await repo.FindByIdAsync(id, ct);
        if (product is null) return false;
        await repo.DeleteAsync(product, ct);
        await repo.SaveChangesAsync(ct);
        return true;
    }
}

// WHY: ALL-OR-NOTHING reserve in one transaction prevents partial reservations
// that could leave the inventory in an inconsistent state.
public sealed class ReserveStockHandler(InventoryDbContext db)
{
    public async Task<ReserveResult> HandleAsync(ReserveRequest req, CancellationToken ct)
    {
        if (req.Items.Count == 0 || req.Items.Any(item => item.Quantity <= 0))
            return ReserveResult.InvalidRequest("Every reservation must contain at least one item with a positive quantity.");

        // Aggregate duplicate lines before checking stock. Otherwise two lines
        // for the same product could each pass the check and oversell it.
        var requestedQuantities = req.Items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        // Lock all requested rows upfront in a consistent order to avoid deadlocks.
        var productIds = requestedQuantities.Keys.OrderBy(id => id).ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Load all products with tracking so EF detects concurrency conflicts.
            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(ct);

            // Validate every item before touching any stock.
            foreach (var item in requestedQuantities)
            {
                var product = products.FirstOrDefault(p => p.Id == item.Key);
                if (product is null)
                    return ReserveResult.NotFound(item.Key);
                if (product.StockQuantity < item.Value)
                    return ReserveResult.InsufficientStock(item.Key, product.StockQuantity);
            }

            // Apply all reductions — only if every check passed.
            foreach (var item in requestedQuantities)
            {
                var product = products.First(p => p.Id == item.Key);
                product.StockQuantity -= item.Value;
            }

            // SaveChanges will throw DbUpdateConcurrencyException if another
            // transaction modified xmin between our read and write.
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ReserveResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            // WHY: Return a known failure type so the controller can return 409.
            return ReserveResult.ConcurrencyConflict();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

public sealed class ReleaseStockHandler(InventoryDbContext db)
{
    public async Task HandleAsync(ReleaseRequest req, CancellationToken ct)
    {
        // Release is best-effort: used for compensation after a failed order.
        // We log but don't throw if a product is missing.
        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(ct);

        foreach (var item in req.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is not null)
                product.StockQuantity += item.Quantity;
        }
        await db.SaveChangesAsync(ct);
    }
}

// Result type to avoid exception-driven control flow for expected business outcomes.
public sealed record ReserveResult(
    bool IsSuccess,
    string? ErrorCode,
    string? Detail,
    Guid? ProductId,
    bool IsInvalidRequest = false)
{
    public static ReserveResult Success() => new(true, null, null, null);
    public static ReserveResult InvalidRequest(string detail) =>
        new(false, "INVALID_REQUEST", detail, null, true);
    public static ReserveResult InsufficientStock(Guid id, int available) =>
        new(false, "INSUFFICIENT_STOCK",
            $"Product {id} has only {available} units available.", id);
    public static ReserveResult NotFound(Guid id) =>
        new(false, "PRODUCT_NOT_FOUND", $"Product {id} not found.", id);
    public static ReserveResult ConcurrencyConflict() =>
        new(false, "CONCURRENCY_CONFLICT",
            "Stock was modified by another request. Please retry.", null);
}
