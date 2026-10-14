namespace LocalHub.Infrastructure.Seed;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string DefaultPassword { get; init; } = string.Empty;
}