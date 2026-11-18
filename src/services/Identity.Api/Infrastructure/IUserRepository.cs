using Identity.Api.Domain;

namespace Identity.Api.Infrastructure;

// WHY: Repository pattern hides EF Core from the application layer.
// Callers depend on the interface, not on EF — makes unit testing easy.
public interface IUserRepository
{
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
