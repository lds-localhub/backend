using LocalHub.Domain.Locations;

namespace LocalHub.UnitTests.Locations;

// The Haversine formula in C# (DEC-06).
[Trait("Category", "Unit")]
public class GeoDistanceTests
{
    // Tolerance of the criterion CA8 of I045.
    private const double ToleranceKm = 0.1;

    // On a meridian, 1° of latitude is R · π / 180 = 111.19 km.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_UmGrauDeLatitude_DevolveADistanciaDeReferencia()
    {
        double distance = GeoDistance.HaversineKm(41.1579m, -8.6291m, 42.1579m, -8.6291m);

        Assert.Equal(111.19, distance, ToleranceKm);
    }
}