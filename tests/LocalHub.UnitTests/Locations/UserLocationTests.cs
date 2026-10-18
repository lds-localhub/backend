using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class UserLocationTests
{
    private static readonly GeoPoint Porto = GeoPoint.Create(41.1579m, -8.6291m);

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public void SetLocation_MoradaECoordenadas_GuardaAsDuasEmConjunto()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        user.SetLocation("  Rua do Almada 10, Porto  ", Porto);

        Assert.Equal("Rua do Almada 10, Porto", user.Address);
        Assert.Equal(41.1579m, user.Latitude);
        Assert.Equal(-8.6291m, user.Longitude);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-03")]
    [InlineData("")]
    [InlineData("   ")]
    public void SetLocation_MoradaVazia_LancaErroNaMorada(string address)
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        var exception = Assert.Throws<DomainException>(() => user.SetLocation(address, Porto));

        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal("address", exception.Field);
        Assert.Null(user.Address);
        Assert.Null(user.Latitude);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public void SetLocation_MoradaComMaisDe255Caracteres_LancaErroNaMorada()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        var exception = Assert.Throws<DomainException>(() => user.SetLocation(new string('a', 256), Porto));

        Assert.Equal("address", exception.Field);
    }
}