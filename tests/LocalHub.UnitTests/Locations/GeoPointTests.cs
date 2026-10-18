using System.Globalization;

using LocalHub.Domain.Common;
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

    [Theory]
    [Trait("Requisito", "RF-F015-04")]
    [InlineData("95", "10")]
    [InlineData("90.000001", "0")]
    [InlineData("-90.000001", "0")]
    public void Create_LatitudeForaDoIntervalo_LancaErroNoCampoLatitude(string latitude, string longitude)
    {
        DomainException ex = Assert.Throws<DomainException>(() => GeoPoint.Create(Parse(latitude), Parse(longitude)));

        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
        Assert.Equal("latitude", ex.Field);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-04")]
    [InlineData("0", "180.000001")]
    [InlineData("0", "-180.000001")]
    [InlineData("41.1579", "200")]
    public void Create_LongitudeForaDoIntervalo_LancaErroNoCampoLongitude(string latitude, string longitude)
    {
        DomainException ex = Assert.Throws<DomainException>(() => GeoPoint.Create(Parse(latitude), Parse(longitude)));

        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
        Assert.Equal("longitude", ex.Field);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public void Create_MaisDeSeisCasasDecimais_ArredondaASeisCasas()
    {
        GeoPoint point = GeoPoint.Create(41.15790049m, -8.62910051m);

        Assert.Equal(41.157900m, point.Latitude);
        Assert.Equal(-8.629101m, point.Longitude);
    }

    private static decimal Parse(string value)
    {
        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }
}