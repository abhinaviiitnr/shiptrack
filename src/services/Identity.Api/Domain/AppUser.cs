// WHY: Keeping domain entities in a separate namespace / folder makes it
// explicit what is "core business data" vs. infrastructure or HTTP concerns.
namespace Identity.Api.Domain;

public sealed class AppUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    // Password is always stored as a hash — never plaintext.
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Customer;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>Application role constants. Keep them in one place to avoid typos.</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}
