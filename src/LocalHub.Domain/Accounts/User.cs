using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Domain.Accounts;

/// <summary>
/// Represents a user of the system: a client, a service provider or an administrator.
/// </summary>
public class User
{
    // Sizes of the columns of the data model (varchar(n)). The DTOs use the same constants (rule R17).
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PasswordHashMaxLength = 255;
    public const int AddressMaxLength = 255;

    // "private set": the values can only be changed by the methods of this class (Register, Deactivate, ...),
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

    // Navigations (they are not columns). The lists are private so that other classes can read them but cannot add
    // or remove items.
    private readonly List<UserSession> _sessions = new();
    public IReadOnlyCollection<UserSession> Sessions => _sessions;
    // The professional profile of a provider; null for clients and administrators.
    public ProfessionalProfile? Profile { get; private set; }
    // The services created by a client.
    private readonly List<Service> _services = new();
    public IReadOnlyCollection<Service> Services => _services;
    // The changes of state of services made by this user.
    private readonly List<ServiceStatusHistory> _statusChanges = new();
    public IReadOnlyCollection<ServiceStatusHistory> StatusChanges => _statusChanges;

    // Private: a user is created with Register or CreateAdministrator. Entity Framework uses this constructor
    // when it reads a user from the database.
    private User() { }

    // Used by the registration (RF-F001-01). The fields, including the role (only Client or Provider), are
    // validated by the DTO (rule R17).
    public static User Register(string name, string email, UserRole role) => new()
    {
        Name = name.Trim(),
        // Always stored in lowercase, so "Ana@Mail.com" and "ana@mail.com" are the same e-mail (RF-F001-01).
        Email = email.Trim().ToLowerInvariant(),
        Role = role,
        Status = ActivationStatus.Active,
    };

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
        PasswordHash = passwordHash;
    }

    // Used by the administrator (RF-F002-02): only an active Client or Provider account can be deactivated.
    // The sessions must be loaded: from now on none of them is accepted (API documentation, E06).
    // The services, direct requests, proposals and reviews of the account keep their states.
    public void Deactivate(DateTimeOffset now)
    {
        if (Role == UserRole.Admin)
        {
            throw new DomainException(DomainErrorKind.Conflict, "Não é possível desativar uma conta de Administrador.");
        }

        if (Status == ActivationStatus.Inactive)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A conta já está desativada.");
        }

        Status = ActivationStatus.Inactive;
        foreach (var session in _sessions)
        {
            session.Revoke(now);
        }
    }

    // Used by the administrator (RF-F002-03): after the activation the user can log in again.
    public void Activate()
    {
        if (Status == ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A conta já está ativa.");
        }

        Status = ActivationStatus.Active;
    }

    // The address and the coordinates are always changed together. The address was validated by the DTO and the
    // coordinates were obtained from the geocoding service (rule R17).
    public void SetLocation(string address, decimal latitude, decimal longitude)
    {
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }
}