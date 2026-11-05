namespace LocalHub.Application.Accounts;

public sealed class SupportOptions
{
    public const string SectionName = "Support";

    public string Email { get; init; } = string.Empty;
}