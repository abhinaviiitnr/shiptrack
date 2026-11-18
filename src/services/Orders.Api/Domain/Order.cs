namespace Orders.Api.Domain;

// WHY: Explicit enum values make migrations and DB columns stable even if the
// enum order changes in source code.
public enum OrderStatus
{
    Confirmed = 1,
    Packed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum ShippingMethod
{
    Standard = 1,
    Express = 2
}

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
    public ShippingMethod ShippingMethod { get; init; }
    public decimal ShippingCost { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; init; } = [];
    public List<OrderStatusHistory> StatusHistory { get; init; } = [];
}

public sealed class OrderItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrderId { get; init; }
    public Guid ProductId { get; init; }
    // Snapshots capture the product's name and price at order time.
    // If the product is later edited, the order record is preserved.
    public string ProductNameSnapshot { get; init; } = string.Empty;
    public decimal UnitPriceSnapshot { get; init; }
    public int Quantity { get; init; }
}

public sealed class OrderStatusHistory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrderId { get; init; }
    public OrderStatus FromStatus { get; init; }
    public OrderStatus ToStatus { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    public string ChangedBy { get; init; } = string.Empty;
}
