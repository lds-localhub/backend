using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;

namespace LocalHub.UnitTests.Domain.Accounts;

public class UserTests
{
    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Provider)]
    public void Register_ClientOrProvider_CreatesActiveAccountWithoutLocation(UserRole role)
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
    public void Register_AdminRole_ThrowsValidationErrorOnRole()
    {
        var exception = Assert.Throws<DomainException>(
            () => User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Admin));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal("role", exception.Field);
    }

    [Fact]
    public void CreateAdministrator_CreatesActiveAdministrator()
    {
        var user = User.CreateAdministrator(" Rui Rocha ", "rui.rocha@localhub.test");

        Assert.Equal("Rui Rocha", user.Name);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal(ActivationStatus.Active, user.Status);
    }

    [Fact]
    public void SetPasswordHash_ValidHash_StoresHash()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        user.SetPasswordHash("hash");

        Assert.Equal("hash", user.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_EmptyHash_ThrowsValidationError(string hash)
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        var exception = Assert.Throws<DomainException>(() => user.SetPasswordHash(hash));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
    }
}