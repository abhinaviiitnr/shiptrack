using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Sku).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.Name).HasMaxLength(500).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.Price).HasPrecision(18, 2);

            // WHY: Map RowVersion to PostgreSQL's system column xmin.
            // xmin changes every time the row is updated, so EF Core uses it
            // as a concurrency token without us adding an extra column.
            e.Property(p => p.RowVersion)
             .HasColumnName("xmin")
             .HasColumnType("xid")
             .ValueGeneratedOnAddOrUpdate()
             .IsConcurrencyToken();
        });
    }
}
