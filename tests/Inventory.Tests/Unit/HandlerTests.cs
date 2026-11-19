using Inventory.Api.Application.Commands;
using Inventory.Api.Application.DTOs;
using Inventory.Api.Domain;
using Inventory.Api.Infrastructure;
using NSubstitute;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Xunit;

namespace Inventory.Tests.Unit;

public class HandlerTests
{
    private readonly IProductRepository _repo = Substitute.For<IProductRepository>();

    [Fact]
    public async Task CreateProduct_HappyPath_ReturnsDto()
    {
        var handler = new CreateProductHandler(_repo);
        var req = new CreateProductRequest("SKU-100", "Mechanical Keyboard", "Clicky keys", 89.99m, 25);

        var result = await handler.HandleAsync(req, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("SKU-100", result.Sku);
        Assert.Equal("Mechanical Keyboard", result.Name);
        Assert.Equal(89.99m, result.Price);
        Assert.Equal(25, result.StockQuantity);
        await _repo.Received(1).AddAsync(Arg.Is<Product>(p => p.Sku == "SKU-100"), Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProduct_ExistingProduct_UpdatesAndReturnsDto()
    {
        var existing = new Product
        {
            Sku = "SKU-200",
            Name = "Old Name",
            Price = 50m,
            StockQuantity = 10
        };
        _repo.FindByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new UpdateProductHandler(_repo);
        var req = new UpdateProductRequest("New Name", "New Desc", 75m, 15);

        var result = await handler.HandleAsync(existing.Id, req, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal(75m, result.Price);
        Assert.Equal(15, result.StockQuantity);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProduct_NotFound_ReturnsNull()
    {
        _repo.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Product?)null);

        var handler = new UpdateProductHandler(_repo);
        var result = await handler.HandleAsync(Guid.NewGuid(), new UpdateProductRequest("A", "B", 10m, 1), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    [Fact]
    public async Task ReserveStock_EmptyOrNonPositiveLines_ReturnsInvalidRequest()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=not-used;Username=not-used;Password=not-used")
            .Options;
        await using var db = new InventoryDbContext(options);
        var handler = new ReserveStockHandler(db);

        var emptyResult = await handler.HandleAsync(new ReserveRequest([]), CancellationToken.None);
        var invalidResult = await handler.HandleAsync(
            new ReserveRequest([new ReserveItem(Guid.NewGuid(), 0)]),
            CancellationToken.None);

        Assert.True(emptyResult.IsInvalidRequest);
        Assert.True(invalidResult.IsInvalidRequest);
        Assert.Equal("INVALID_REQUEST", invalidResult.ErrorCode);
    }
}
