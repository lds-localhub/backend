using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Domain.Accounts;

/// <summary>
/// A user of the system: a client, a service provider or an administrator.
/// </summary>
public class User
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PasswordHashMaxLength = 255;
    public const int AddressMaxLength = 255;

    public int UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public ActivationStatus Status { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    // Navigations. The lists are private so that other classes can read them but cannot change them.
    private readonly List<UserSession> _sessions = new();
    public IReadOnlyCollection<UserSession> Sessions => _sessions;
    public ProfessionalProfile? Profile { get; private set; }
    private readonly List<Service> _services = new();
    public IReadOnlyCollection<Service> Services => _services;
    private readonly List<ServiceStatusHistory> _statusChanges = new();
    public IReadOnlyCollection<ServiceStatusHistory> StatusChanges => _statusChanges;

    // Used by Entity Framework; a user is created with Register or CreateAdministrator.
    private User() { }

    public static User Register(string name, string email, UserRole role, string password,
        Func<User, string, string> hashPassword) => Create(name, email, role, password, hashPassword);

    public static User CreateAdministrator(string name, string email, string password,
        Func<User, string, string> hashPassword) => Create(name, email, UserRole.Admin, password, hashPassword);

    private static User Create(string name, string email, UserRole role, string password,
        Func<User, string, string> hashPassword)
    {
        var user = new User
        {
            Name = name.Trim(),
            // Stored in lowercase so that e-mails are case-insensitive.
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            Status = ActivationStatus.Active,
        };
        user.SetPasswordHash(hashPassword(user, password));
        return user;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("The password hash cannot be empty.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    // The sessions must be loaded: they are all revoked.
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

    public void Activate()
    {
        if (Status == ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A conta já está ativa.");
        }

        Status = ActivationStatus.Active;
    }

    public void SetLocation(string address, decimal latitude, decimal longitude)
    {
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }
}