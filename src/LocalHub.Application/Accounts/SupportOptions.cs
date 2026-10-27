namespace LocalHub.Application.Accounts;

// Support contact shown to the user, for example when a deactivated account tries to log in (RF-F002-04).
// It comes from the configuration (Support:Email) and is not written in the code.
public sealed class SupportOptions
{
    public const string SectionName = "Support";

    public string Email { get; init; } = string.Empty;
}