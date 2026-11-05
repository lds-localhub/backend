namespace LocalHub.Domain.Locations;

/// <summary>
/// Straight-line distance between two points on the Earth (Haversine formula).
/// </summary>
public static class GeoDistance
{
    public const double EarthRadiusKm = 6371.0;

    public static double HaversineKm(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        double phi1 = ToRadians(latitude1);
        double phi2 = ToRadians(latitude2);
        double lambda1 = ToRadians(longitude1);
        double lambda2 = ToRadians(longitude2);

        double a = Math.Pow(Math.Sin((phi2 - phi1) / 2), 2)
                   + (Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin((lambda2 - lambda1) / 2), 2));

        // Rounding errors can push the value slightly above 1.
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static decimal RoundKm(double km) => Math.Round((decimal)km, 1, MidpointRounding.AwayFromZero);

    private static double ToRadians(decimal degrees) => double.DegreesToRadians((double)degrees);
}