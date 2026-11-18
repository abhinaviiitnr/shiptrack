namespace Orders.Api.Domain;

// WHY: State pattern — the allowed transitions are defined in one place.
// Any code that tries an invalid transition gets a clear exception instead of
// silently corrupting the order state.
public static class OrderStateMachine
{
    // Explicit table of valid (From, To) pairs.
    // Interviewers love to ask: "how did you prevent invalid status changes?"
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> AllowedTransitions =
    [
        (OrderStatus.Confirmed, OrderStatus.Packed),
        (OrderStatus.Packed,    OrderStatus.Shipped),
        (OrderStatus.Shipped,   OrderStatus.Delivered),
        (OrderStatus.Confirmed, OrderStatus.Cancelled),
        (OrderStatus.Packed,    OrderStatus.Cancelled),
    ];

    public static bool CanTransition(OrderStatus from, OrderStatus to)
        => AllowedTransitions.Contains((from, to));

    // Returns the set of statuses an order can move to from its current status.
    public static IReadOnlySet<OrderStatus> GetValidNextStatuses(OrderStatus current)
        => AllowedTransitions
            .Where(t => t.From == current)
            .Select(t => t.To)
            .ToHashSet();
}
