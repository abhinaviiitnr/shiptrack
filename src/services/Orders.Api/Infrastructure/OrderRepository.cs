using Microsoft.EntityFrameworkCore;
using Orders.Api.Domain;

namespace Orders.Api.Infrastructure;

public sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    public Task<Order?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Orders
             .Include(o => o.Items)
             .Include(o => o.StatusHistory)
             .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await db.Orders.AddAsync(order, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    // WHY: Keyset pagination (cursor on Id) is O(1) regardless of page depth.
    // OFFSET pagination is O(n) — it scans all skipped rows, which is slow on
    // large tables. With 300,000 orders, OFFSET for page 500 reads 10,000 rows
    // before returning 20. Keyset reads exactly 20.
    public async Task<(IReadOnlyList<OrderSummaryDto> Items, Guid? NextCursor)> GetAdminOrdersAsync(
        AdminOrderFilter filter, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking()
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(o => o.Status == filter.Status.Value);
        if (filter.From.HasValue)
            query = query.Where(o => o.CreatedAt >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(o => o.CreatedAt <= filter.To.Value);
        if (filter.CustomerId.HasValue)
            query = query.Where(o => o.CustomerId == filter.CustomerId.Value);

        // Keyset: return rows with Id > cursor, ordered by CreatedAt DESC then Id.
        if (filter.Cursor.HasValue)
        {
            var cursor = await db.Orders
                .AsNoTracking()
                .Where(o => o.Id == filter.Cursor.Value)
                .Select(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            query = query.Where(o => o.CreatedAt < cursor
                || (o.CreatedAt == cursor && o.Id.CompareTo(filter.Cursor.Value) > 0));
        }

        // WHY: Projection into DTOs avoids loading navigation properties we don't need.
        // AsNoTracking + projection = fastest possible read-only query.
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Take(filter.PageSize + 1)  // Take one extra to detect if there's a next page.
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.CustomerId,
                o.Status.ToString(),
                o.ShippingMethod.ToString(),
                o.TotalAmount,
                o.CreatedAt))
            .ToListAsync(ct);

        Guid? nextCursor = null;
        if (items.Count > filter.PageSize)
        {
            nextCursor = items[filter.PageSize - 1].Id;
            items.RemoveAt(filter.PageSize);
        }

        return (items, nextCursor);
    }

    public async Task<(IReadOnlyList<OrderSummaryDto> Items, Guid? NextCursor)> GetCustomerOrdersAsync(
        Guid customerId, Guid? cursor, int pageSize, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId);

        if (cursor.HasValue)
        {
            var cursorDate = await db.Orders.AsNoTracking()
                .Where(o => o.Id == cursor.Value)
                .Select(o => o.CreatedAt)
                .FirstOrDefaultAsync(ct);

            query = query.Where(o => o.CreatedAt < cursorDate
                || (o.CreatedAt == cursorDate && o.Id.CompareTo(cursor.Value) > 0));
        }

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Take(pageSize + 1)
            .Select(o => new OrderSummaryDto(
                o.Id, o.CustomerId, o.Status.ToString(),
                o.ShippingMethod.ToString(), o.TotalAmount, o.CreatedAt))
            .ToListAsync(ct);

        Guid? nextCursor = null;
        if (items.Count > pageSize)
        {
            nextCursor = items[pageSize - 1].Id;
            items.RemoveAt(pageSize);
        }

        return (items, nextCursor);
    }
}
