using LocalHub.Domain.Locations;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class GeoPointTests
{
    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public void Create_CoordenadasDoCriterioDeVerificacao_SaoAceites()
    {
        GeoPoint point = GeoPoint.Create(41.1579m, -8.6291m);

        Assert.Equal(41.1579m, point.Latitude);
        Assert.Equal(-8.6291m, point.Longitude);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-04")]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Create_ValoresLimite_SaoAceites(int latitude, int longitude)
    {
        GeoPoint point = GeoPoint.Create(latitude, longitude);

        Assert.Equal((decimal)latitude, point.Latitude);
        Assert.Equal((decimal)longitude, point.Longitude);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public void Create_MaisDeSeisCasasDecimais_ArredondaASeisCasas()
    {
        GeoPoint point = GeoPoint.Create(41.15790049m, -8.62910051m);

        Assert.Equal(41.157900m, point.Latitude);
        Assert.Equal(-8.629101m, point.Longitude);
    }
}