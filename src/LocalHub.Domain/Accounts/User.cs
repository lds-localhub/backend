using LocalHub.Domain.Common;

namespace LocalHub.Domain.Accounts;

public class User
{
    public int UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public ActivationStatus Status { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    private readonly List<UserSession> _sessions = [];
    public IReadOnlyCollection<UserSession> Sessions => _sessions;

    private User() { }

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
            Email = email.Trim(),
            Role = role,
            Status = ActivationStatus.Active,
        };
    }


    public static User CreateAdministrator(string name, string email) => new()
    {
        Name = name.Trim(),
        Email = email.Trim(),
        Role = UserRole.Admin,
        Status = ActivationStatus.Active,
    };

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(DomainErrorKind.Validation, "O resumo da palavra-passe é obrigatório.");
        }

        PasswordHash = passwordHash;
    }
}