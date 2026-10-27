using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;

namespace LocalHub.UnitTests.Domain.Accounts;

[Trait("Category", "Unit")]
public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Provider)]
    [Trait("Requisito", "RF-F001-01")]
    public void Register_ClienteOuPrestador_CriaContaAtivaSemLocalizacao(UserRole role)
    {
        var user = User.Register("  Ana Costa  ", "  ana.costa@exemplo.pt ", role);

        Assert.Equal("Ana Costa", user.Name);
        Assert.Equal("ana.costa@exemplo.pt", user.Email);
        Assert.Equal(role, user.Role);
        Assert.Equal(ActivationStatus.Active, user.Status);
        Assert.Null(user.Address);
        Assert.Null(user.Latitude);
        Assert.Null(user.Longitude);
        Assert.Empty(user.Sessions);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-05")]
    public void CreateAdministrator_CriaAdministradorAtivo()
    {
        var user = User.CreateAdministrator(" Rui Rocha ", "rui.rocha@localhub.test");

        Assert.Equal("Rui Rocha", user.Name);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal(ActivationStatus.Active, user.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public void SetPasswordHash_ResumoValido_GuardaResumo()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        user.SetPasswordHash("hash");

        Assert.Equal("hash", user.PasswordHash);
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Provider)]
    [Trait("Requisito", "RF-F002-02")]
    public void Deactivate_ContaAtivaDeClienteOuPrestador_FicaDesativada(UserRole role)
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", role);

        user.Deactivate(Now);

        Assert.Equal(ActivationStatus.Inactive, user.Status);
        Assert.Equal(role, user.Role);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public void Deactivate_Administrador_LancaConflitoEMantemContaAtiva()
    {
        var user = User.CreateAdministrator("Rui Rocha", "rui.rocha@localhub.test");

        var exception = Assert.Throws<DomainException>(() => user.Deactivate(Now));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Equal(ActivationStatus.Active, user.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public void Deactivate_ContaJaDesativada_LancaConflito()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);
        user.Deactivate(Now);

        var exception = Assert.Throws<DomainException>(() => user.Deactivate(Now.AddHours(1)));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Equal(ActivationStatus.Inactive, user.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-03")]
    public void Activate_ContaDesativada_FicaAtiva()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Provider);
        user.Deactivate(Now);

        user.Activate();

        Assert.Equal(ActivationStatus.Active, user.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-03")]
    public void Activate_ContaAtiva_LancaConflito()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        var exception = Assert.Throws<DomainException>(() => user.Activate());

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
    }
}