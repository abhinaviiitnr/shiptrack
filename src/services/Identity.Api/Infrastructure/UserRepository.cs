using Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Infrastructure;

// WHY: The concrete repository uses EF Core. If we ever swap databases
// we only change this file — the application layer is untouched.
public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default)
        => db.Users
             .AsNoTracking()  // read-only query — no need to track the entity
             .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
