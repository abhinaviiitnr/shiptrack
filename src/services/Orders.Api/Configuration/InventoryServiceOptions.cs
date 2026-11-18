namespace Orders.Api.Configuration;

public sealed class InventoryServiceOptions
{
    public const string SectionName = "InventoryService";
    public string BaseUrl { get; init; } = string.Empty;
    public string InternalKey { get; init; } = string.Empty;
}
