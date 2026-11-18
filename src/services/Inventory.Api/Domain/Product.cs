namespace Inventory.Api.Domain;

// WHY: Domain entities contain business data and rules, not HTTP or database concerns.
public sealed class Product
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // WHY: xmin is PostgreSQL's internal row version — increments on every update.
    // We use it for optimistic concurrency to prevent two requests from both
    // decrementing stock from the same value (which would allow overselling).
    public uint RowVersion { get; set; }  // mapped to xmin in EF config
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}
