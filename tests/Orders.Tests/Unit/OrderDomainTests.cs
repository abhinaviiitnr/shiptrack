using Orders.Api.Application.DTOs;
using Orders.Api.Application.Validators;
using Orders.Api.Domain;
using Xunit;

namespace Orders.Tests.Unit;

public sealed class OrderDomainTests
{
    [Theory]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Packed, true)]
    [InlineData(OrderStatus.Packed, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Packed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    public void StateMachine_EnforcesTransitionTable(
        OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, OrderStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(499.99, 50)]
    [InlineData(1000, 0)]
    public void StandardShipping_AppliesFreeShippingThreshold(
        decimal itemsTotal, decimal expected)
    {
        Assert.Equal(expected, new StandardShipping().Calculate(itemsTotal));
    }

    [Fact]
    public void ExpressShipping_IsAlways150()
    {
        Assert.Equal(150m, new ExpressShipping().Calculate(5000m));
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1001, false)]
    [InlineData(1, 2, true)]
    public async Task CreateOrderValidator_ValidatesItems(
        int itemCount, int quantity, bool expected)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(_ => new OrderItemRequest(Guid.NewGuid(), quantity))
            .ToList();

        var result = await new CreateOrderRequestValidator()
            .ValidateAsync(new CreateOrderRequest(items, ShippingMethodDto.Standard));

        Assert.Equal(expected, result.IsValid);
    }
}
