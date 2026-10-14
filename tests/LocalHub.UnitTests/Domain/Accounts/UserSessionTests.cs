using LocalHub.Domain.Accounts;

namespace LocalHub.UnitTests.Domain.Accounts;

[Trait("Category", "Unit")]
public class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static UserSession StartSession() =>
        UserSession.Start(User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client), Now);

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public void Start_ContaAtiva_CriaSessaoValidaDuranteOitoHoras()
    {
        var session = StartSession();

        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now.AddHours(8), session.ExpiresAt);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public void IsValid_AntesDeExpirar_DevolveTrue()
    {
        var session = StartSession();

        Assert.True(session.IsValid(Now.AddHours(8).AddTicks(-1)));
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public void IsValid_NoMomentoOuDepoisDeExpirar_DevolveFalse()
    {
        var session = StartSession();

        Assert.False(session.IsValid(Now.AddHours(8)));
        Assert.False(session.IsValid(Now.AddHours(9)));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-04")]
    public void Revoke_SessaoValida_TornaSessaoInvalida()
    {
        var session = StartSession();

        session.Revoke(Now.AddMinutes(30));

        Assert.Equal(Now.AddMinutes(30), session.RevokedAt);
        Assert.False(session.IsValid(Now.AddMinutes(31)));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-04")]
    public void Revoke_SessaoJaRevogada_MantemPrimeiraDataDeRevogacao()
    {
        var session = StartSession();

        session.Revoke(Now.AddMinutes(30));
        session.Revoke(Now.AddHours(2));

        Assert.Equal(Now.AddMinutes(30), session.RevokedAt);
    }
}