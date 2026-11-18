using Inventory.Api.Domain;

namespace Inventory.Api.Application.DTOs;

public static class ProductMappingExtensions
{
    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Price, p.StockQuantity, p.CreatedAt);
}
