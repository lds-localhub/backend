using LocalHub.Domain.Common;

namespace LocalHub.Domain.Accounts;

/// <summary>
/// Represents a user of the system: a client, a service provider or an administrator.
/// </summary>
public class User
{
    // "private set": the values can only be changed by the methods of this class (Register, SetPasswordHash, ...),
    // so the rules below are always applied. Entity Framework can still fill them when it reads from the database.
    public int UserId { get; private set; }
    // "= null!" tells the compiler that these are never null: they are always set by Register or by Entity Framework.
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public ActivationStatus Status { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    // The list is private so that other classes can read the sessions but cannot add or remove them.
    private readonly List<UserSession> _sessions = [];
    public IReadOnlyCollection<UserSession> Sessions => _sessions;

    // Private: a user is created with Register or CreateAdministrator. Entity Framework uses this constructor
    // when it reads a user from the database.
    private User() { }

    // Used by the registration (RF-F001-01): only Client or Provider can be chosen.
    public static User Register(string name, string email, UserRole role)
    {
        if (role == UserRole.Admin)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "O papel tem de ser Cliente ou Prestador de Serviços.", "role");
        }

        return new User
        {
            Name = name.Trim(),
            // Always stored in lowercase, so "Ana@Mail.com" and "ana@mail.com" are the same e-mail (RF-F001-01).
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            Status = ActivationStatus.Active,
        };
    }


    // Administrators cannot register themselves; they are created by the DataSeeder.
    public static User CreateAdministrator(string name, string email) => new()
    {
        Name = name.Trim(),
        Email = email.Trim().ToLowerInvariant(),
        Role = UserRole.Admin,
        Status = ActivationStatus.Active,
    };

    // Receives the hash created by IPasswordHasher, never the password itself.
    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(DomainErrorKind.Validation, "O resumo da palavra-passe é obrigatório.");
        }

        PasswordHash = passwordHash;
    }
}