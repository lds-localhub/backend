namespace LocalHub.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "LocalHub";
    public string Audience { get; init; } = "LocalHub";
    public string SigningKey { get; init; } = string.Empty;
}