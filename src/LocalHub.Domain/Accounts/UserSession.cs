using LocalHub.Domain.Common;

namespace LocalHub.Domain.Accounts;

/// <summary>
/// Represents a login session of a user. The JWT token returned at login identifies this session.
/// </summary>
public class UserSession
{
    // A session is valid for 8 hours (RNF-02).
    public static readonly TimeSpan Duration = TimeSpan.FromHours(8);

    // Private: a session is created with Start. Entity Framework uses this constructor
    // when it reads a session from the database.
    private UserSession() { }

    public int SessionId { get; private set; }
    public int UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // Navigation (not a column). "= null!": loaded by Entity Framework with Include.
    public User User { get; private set; } = null!;

    public static UserSession Start(User user, DateTimeOffset now)
    {
        if (user.Status != ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Forbidden, "A conta está desativada.");
        }

        // SessionId is not set here: the database generates it when the session is saved.
        return new UserSession
        {
            UserId = user.UserId,
            CreatedAt = now,
            ExpiresAt = now + Duration,
        };
    }

    // Valid while it was not revoked (logout or deactivated account) and has not expired.
    public bool IsValid(DateTimeOffset now)
    {
        return RevokedAt is null && now < ExpiresAt;
    }

    public void Revoke(DateTimeOffset now)
    {
        // A session that was already revoked keeps its original revocation date.
        if (RevokedAt is null)
        {
            RevokedAt = now;
        }
    }
}
