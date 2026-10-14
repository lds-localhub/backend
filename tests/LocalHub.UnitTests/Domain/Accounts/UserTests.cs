using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;

namespace LocalHub.UnitTests.Domain.Accounts;

[Trait("Category", "Unit")]
public class UserTests
{
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
    [Trait("Requisito", "RF-F001-01")]
    public void Register_PapelAdministrador_LancaErroDeValidacaoNoPapel()
    {
        var exception = Assert.Throws<DomainException>(
            () => User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Admin));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal("role", exception.Field);
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
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requisito", "RF-F001-01")]
    public void SetPasswordHash_ResumoVazio_LancaErroDeValidacao(string hash)
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        var exception = Assert.Throws<DomainException>(() => user.SetPasswordHash(hash));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
    }
}