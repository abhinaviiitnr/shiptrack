using Inventory.Api.Application.DTOs;
using Inventory.Api.Application.Validators;
using Xunit;

namespace Inventory.Tests.Unit;

public class ValidatorTests
{
    private readonly CreateProductRequestValidator _createValidator = new();
    private readonly UpdateProductRequestValidator _updateValidator = new();

    [Theory]
    [InlineData("SKU-001", "Gaming Laptop", "Fast laptop", 1299.99, 10, true)]
    [InlineData("", "Gaming Laptop", "Fast laptop", 1299.99, 10, false)] // empty SKU
    [InlineData("SKU-001", "", "Fast laptop", 1299.99, 10, false)] // empty Name
    [InlineData("SKU-001", "Gaming Laptop", "Fast laptop", 0, 10, false)] // price <= 0
    [InlineData("SKU-001", "Gaming Laptop", "Fast laptop", -10, 10, false)] // negative price
    [InlineData("SKU-001", "Gaming Laptop", "Fast laptop", 99.99, -1, false)] // negative stock
    public async Task CreateProductValidator_ValidatesCorrectly(
        string sku, string name, string description, decimal price, int stock, bool expectedValid)
    {
        var request = new CreateProductRequest(sku, name, description, price, stock);
        var result = await _createValidator.ValidateAsync(request);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("Gaming Laptop Pro", "Updated description", 1499.99, 5, true)]
    [InlineData("", "Updated description", 1499.99, 5, false)]
    [InlineData("Gaming Laptop Pro", "Updated description", 0, 5, false)]
    [InlineData("Gaming Laptop Pro", "Updated description", 1499.99, -1, false)]
    public async Task UpdateProductValidator_ValidatesCorrectly(
        string name, string description, decimal price, int stock, bool expectedValid)
    {
        var request = new UpdateProductRequest(name, description, price, stock);
        var result = await _updateValidator.ValidateAsync(request);
        Assert.Equal(expectedValid, result.IsValid);
    }
}
