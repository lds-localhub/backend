using LocalHub.Domain.Accounts;

namespace LocalHub.UnitTests;

// Creates users for the unit tests with a fixed hash, so the tests do not depend on IPasswordHasher.
internal static class TestUsers
{
    public const string Password = "casa2026";
    public const string PasswordHash = "hash";

    public static User Register(string name, string email, UserRole role) =>
        User.Register(name, email, role, Password, (_, _) => PasswordHash);

    public static User CreateAdministrator(string name, string email) =>
        User.CreateAdministrator(name, email, Password, (_, _) => PasswordHash);
}
