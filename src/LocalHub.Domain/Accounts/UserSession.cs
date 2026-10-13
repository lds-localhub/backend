using LocalHub.Domain.Common;

namespace LocalHub.Domain.Accounts;

public class UserSession
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(8);

    private UserSession() { }

    public int SessionId { get; private set; }
    public int UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static UserSession Start(User user, DateTimeOffset now)
    {
        if (user.Status != ActivationStatus.Active)
        {
            throw new DomainException(DomainErrorKind.Forbidden, "A conta está desativada.");
        }

        return new UserSession
        {
            UserId = user.UserId,
            CreatedAt = now,
            ExpiresAt = now + Duration,
        };
    }

    public bool IsValid(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}