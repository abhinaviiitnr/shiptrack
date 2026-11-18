namespace Inventory.Api.Configuration;

public sealed class InternalKeyOptions
{
    public const string SectionName = "InternalKey";
    public string Value { get; init; } = string.Empty;
}
