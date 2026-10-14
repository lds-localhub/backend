using LocalHub.Domain.Accounts;

namespace LocalHub.UnitTests.Domain.Accounts;

public class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static UserSession StartSession() =>
        UserSession.Start(User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client), Now);

    [Fact]
    public void Start_ActiveAccount_CreatesSessionValidForEightHours()
    {
        var session = StartSession();

        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now.AddHours(8), session.ExpiresAt);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public void IsValid_BeforeExpiration_ReturnsTrue()
    {
        var session = StartSession();

        Assert.True(session.IsValid(Now.AddHours(8).AddTicks(-1)));
    }

    [Fact]
    public void IsValid_AtOrAfterExpiration_ReturnsFalse()
    {
        var session = StartSession();

        Assert.False(session.IsValid(Now.AddHours(8)));
        Assert.False(session.IsValid(Now.AddHours(9)));
    }

    [Fact]
    public void Revoke_ValidSession_MakesSessionInvalid()
    {
        var session = StartSession();

        session.Revoke(Now.AddMinutes(30));

        Assert.Equal(Now.AddMinutes(30), session.RevokedAt);
        Assert.False(session.IsValid(Now.AddMinutes(31)));
    }

    [Fact]
    public void Revoke_AlreadyRevokedSession_KeepsFirstRevocationTime()
    {
        var session = StartSession();

        session.Revoke(Now.AddMinutes(30));
        session.Revoke(Now.AddHours(2));

        Assert.Equal(Now.AddMinutes(30), session.RevokedAt);
    }
}