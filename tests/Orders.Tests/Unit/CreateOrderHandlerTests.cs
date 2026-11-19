using Microsoft.Extensions.Logging;
using NSubstitute;
using Orders.Api.Application.Commands;
using Orders.Api.Application.DTOs;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;
using Xunit;

namespace Orders.Tests.Unit;

public sealed class CreateOrderHandlerTests
{
    private readonly IInventoryClient _inventory = Substitute.For<IInventoryClient>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ShippingStrategySelector _shipping = new(
        [new StandardShipping(), new ExpressShipping()]);
    private readonly ILogger<CreateOrderHandler> _logger =
        Substitute.For<ILogger<CreateOrderHandler>>();

    [Fact]
    public async Task HandleAsync_ReservesAndSavesOrder()
    {
        var productId = Guid.NewGuid();
        ConfigureProduct(productId, 25m);
        _inventory.ReserveAsync(Arg.Any<IReadOnlyList<ReserveItemDto>>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryReserveResult(true, null));

        var result = await CreateHandler().HandleAsync(
            Request(productId, 2), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Order!.TotalAmount);
        await _inventory.Received(1).ReserveAsync(
            Arg.Is<IReadOnlyList<ReserveItemDto>>(items =>
                items.Count == 1 && items[0].ProductId == productId && items[0].Quantity == 2),
            Arg.Any<CancellationToken>());
        await _orders.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _inventory.DidNotReceive().ReleaseAsync(
            Arg.Any<IReadOnlyList<ReserveItemDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenSaveFails_ReleasesReservedStock()
    {
        var productId = Guid.NewGuid();
        ConfigureProduct(productId, 25m);
        _inventory.ReserveAsync(Arg.Any<IReadOnlyList<ReserveItemDto>>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryReserveResult(true, null));
        _orders.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database failure")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().HandleAsync(
                Request(productId, 1), Guid.NewGuid(), CancellationToken.None));

        await _inventory.Received(1).ReleaseAsync(
            Arg.Is<IReadOnlyList<ReserveItemDto>>(items =>
                items.Count == 1 && items[0].ProductId == productId && items[0].Quantity == 1),
            CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenInventoryReportsInsufficientStock_DoesNotSave()
    {
        var productId = Guid.NewGuid();
        ConfigureProduct(productId, 25m);
        _inventory.ReserveAsync(Arg.Any<IReadOnlyList<ReserveItemDto>>(), Arg.Any<CancellationToken>())
            .Returns(new InventoryReserveResult(false, "Not enough stock."));

        var result = await CreateHandler().HandleAsync(
            Request(productId, 1), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INSUFFICIENT_STOCK", result.ErrorCode);
        await _orders.DidNotReceive().AddAsync(
            Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    private void ConfigureProduct(Guid id, decimal price)
    {
        _inventory.GetProductPricesAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new ProductPriceDto(
                id, "SKU-1", "Test product", "Description", price, 10, DateTime.UtcNow)]);
    }

    private CreateOrderHandler CreateHandler()
        => new(_inventory, _orders, _shipping, _logger);

    private static CreateOrderRequest Request(Guid productId, int quantity)
        => new([new OrderItemRequest(productId, quantity)], ShippingMethodDto.Standard);
}
