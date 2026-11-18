using Microsoft.EntityFrameworkCore;
using Identity.Api.Domain;

namespace Identity.Api.Infrastructure;

// WHY: DbContext is the EF Core unit-of-work. One DbContext per service
// enforces database-per-service isolation — Orders cannot query AppUsers.
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            // Unique index ensures no duplicate emails, enforced at the database level.
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasMaxLength(50).IsRequired();
        });
    }
}
