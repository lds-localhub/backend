using LocalHub.Domain.Locations;

namespace LocalHub.UnitTests.Locations;

// The Haversine formula in C# (DEC-06). The same formula in SQL, calculated by PostgreSQL, is tested in
// SearchEndpointsTests with the same reference values.
[Trait("Category", "Unit")]
public class GeoDistanceTests
{
    // Tolerance of the criterion CA8 of I045.
    private const double ToleranceKm = 0.1;

    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_MesmoPonto_DevolveZero()
    {
        Assert.Equal(0, GeoDistance.HaversineKm(41.1579m, -8.6291m, 41.1579m, -8.6291m));
    }

    // On a meridian, 1° of latitude is R · π / 180 = 111.19 km.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_UmGrauDeLatitude_DevolveADistanciaDeReferencia()
    {
        double distance = GeoDistance.HaversineKm(41.1579m, -8.6291m, 42.1579m, -8.6291m);

        Assert.Equal(111.19, distance, ToleranceKm);
    }

    // Known reference value: Land's End (50° 03′ 59″ N, 5° 42′ 53″ W) to John o' Groats (58° 38′ 38″ N, 3° 04′ 12″ W)
    // is 968.9 km with the Haversine formula and R = 6371 km (example of https://www.movable-type.co.uk/scripts/latlong.html).
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_LandsEndAJohnOGroats_DevolveADistanciaDeReferencia()
    {
        double distance = GeoDistance.HaversineKm(50.066389m, -5.714722m, 58.643889m, -3.07m);

        Assert.Equal(968.9, distance, ToleranceKm);
    }

    // On the equator, 90° of longitude is a quarter of the circumference: π · R / 2 = 10007.5 km.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_NoventaGrausNoEquador_DevolveUmQuartoDaCircunferencia()
    {
        double distance = GeoDistance.HaversineKm(0m, 0m, 0m, 90m);

        Assert.Equal(Math.PI * GeoDistance.EarthRadiusKm / 2, distance, ToleranceKm);
    }

    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_PontosTrocados_DevolveAMesmaDistancia()
    {
        double there = GeoDistance.HaversineKm(41.1579m, -8.6291m, 38.7223m, -9.1393m);
        double back = GeoDistance.HaversineKm(38.7223m, -9.1393m, 41.1579m, -8.6291m);

        Assert.Equal(there, back, 1e-9);
    }

    // For these points on opposite sides of the Earth the value under the square root is slightly greater than 1
    // (rounding errors), so without Math.Min the arcsine would be NaN. The result is half of the circumference, π · R.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public void HaversineKm_PontosOpostosDaTerra_DevolveMetadeDaCircunferencia()
    {
        double distance = GeoDistance.HaversineKm(87.5m, 180m, -87.5m, 0m);

        Assert.Equal(Math.PI * GeoDistance.EarthRadiusKm, distance, ToleranceKm);
    }

    // The distance is shown with one decimal place (API documentation, section 4.3); 0.05 rounds up.
    [Theory]
    [Trait("Requisito", "RF-F005-01")]
    [InlineData(2.44, 2.4)]
    [InlineData(2.45, 2.5)]
    [InlineData(9.99998, 10.0)]
    [InlineData(0, 0)]
    public void RoundKm_Distancia_ArredondaAUmaCasaDecimal(double km, double expected)
    {
        Assert.Equal((decimal)expected, GeoDistance.RoundKm(km));
    }
}