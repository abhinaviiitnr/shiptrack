// WHY: Options pattern gives strongly-typed, validated configuration objects
// instead of raw IConfiguration["Key"] strings scattered everywhere.
namespace Identity.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 15;
}
