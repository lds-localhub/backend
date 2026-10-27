namespace LocalHub.Domain.Locations;

/// <summary>
/// Calculates the straight-line distance between two points on the Earth with the Haversine formula
/// (Architecture documentation, DEC-06).
/// </summary>
public static class GeoDistance
{
    // Mean radius of the Earth.
    public const double EarthRadiusKm = 6371.0;

    // In a query, Entity Framework does not run this code: LocalHubDbContext maps the method to the same formula in SQL,
    // so the distance is calculated by PostgreSQL and the results can be ordered and paged by it (E18; later E19 and
    // E22). Called in C#, it runs the code below.
    public static double HaversineKm(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        double phi1 = ToRadians(latitude1);
        double phi2 = ToRadians(latitude2);
        double lambda1 = ToRadians(longitude1);
        double lambda2 = ToRadians(longitude2);

        double a = Math.Pow(Math.Sin((phi2 - phi1) / 2), 2)
                   + (Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin((lambda2 - lambda1) / 2), 2));

        // For points on opposite sides of the Earth, the rounding errors can make the square root slightly greater than 1,
        // and the arcsine of a value greater than 1 does not exist.
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    // The distances are shown in kilometres with one decimal place (API documentation, section 4.3).
    public static decimal RoundKm(double km) => Math.Round((decimal)km, 1, MidpointRounding.AwayFromZero);

    private static double ToRadians(decimal degrees) => double.DegreesToRadians((double)degrees);
}