using Microsoft.EntityFrameworkCore;
using Orders.Api.Domain;

namespace Orders.Api.Infrastructure;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Status).IsRequired();
            e.Property(o => o.ShippingMethod).IsRequired();
            e.Property(o => o.ShippingCost).HasPrecision(18, 2);
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);

            // Composite indexes for the two key query patterns.
            // WHY: See docs/sql-optimization.md for EXPLAIN ANALYZE output.
            e.HasIndex(o => new { o.Status, o.CreatedAt });
            e.HasIndex(o => new { o.CustomerId, o.CreatedAt });

            e.HasMany(o => o.Items)
             .WithOne()
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(o => o.StatusHistory)
             .WithOne()
             .HasForeignKey(h => h.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.ProductNameSnapshot).HasMaxLength(500).IsRequired();
            e.Property(i => i.UnitPriceSnapshot).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrderStatusHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.ChangedBy).HasMaxLength(256);
        });
    }
}
