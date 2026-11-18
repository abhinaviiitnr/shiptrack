namespace Orders.Api.Domain;

// WHY: Strategy pattern — the shipping cost calculation algorithm is swappable.
// Adding a new shipping method means adding a new class, not editing existing code.
// (Open/Closed Principle)
public interface IShippingStrategy
{
    ShippingMethod Method { get; }
    decimal Calculate(decimal itemsTotal);
}

/// <summary>
/// Standard shipping: flat 50, free when order total >= 1000.
/// </summary>
public sealed class StandardShipping : IShippingStrategy
{
    public ShippingMethod Method => ShippingMethod.Standard;

    public decimal Calculate(decimal itemsTotal)
        => itemsTotal >= 1000m ? 0m : 50m;
}

/// <summary>
/// Express shipping: always 150.
/// </summary>
public sealed class ExpressShipping : IShippingStrategy
{
    public ShippingMethod Method => ShippingMethod.Express;

    public decimal Calculate(decimal itemsTotal) => 150m;
}

/// <summary>
/// Dispatcher: picks the right strategy for the requested shipping method.
/// </summary>
public sealed class ShippingStrategySelector(IEnumerable<IShippingStrategy> strategies)
{
    private readonly Dictionary<ShippingMethod, IShippingStrategy> _map =
        strategies.ToDictionary(s => s.Method);

    public IShippingStrategy GetStrategy(ShippingMethod method)
        => _map.TryGetValue(method, out var s) ? s
           : throw new ArgumentOutOfRangeException(nameof(method), $"Unknown shipping method: {method}");
}
