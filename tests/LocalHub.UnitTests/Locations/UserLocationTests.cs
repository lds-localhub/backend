using LocalHub.Domain.Accounts;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class UserLocationTests
{
    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public void SetLocation_MoradaECoordenadas_GuardaAsDuasEmConjunto()
    {
        var user = User.Register("Ana Costa", "ana.costa@exemplo.pt", UserRole.Client);

        user.SetLocation("  Rua do Almada 10, Porto  ", 41.1579m, -8.6291m);

        Assert.Equal("Rua do Almada 10, Porto", user.Address);
        Assert.Equal(41.1579m, user.Latitude);
        Assert.Equal(-8.6291m, user.Longitude);
    }
}