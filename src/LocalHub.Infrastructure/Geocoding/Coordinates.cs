namespace LocalHub.Infrastructure.Geocoding;

public record Coordinates(decimal Latitude, decimal Longitude)
{
    public const int DecimalPlaces = 6;

    // Same precision as the database columns.
    public static Coordinates Rounded(decimal latitude, decimal longitude)
    {
        return new Coordinates(
            Math.Round(latitude, DecimalPlaces, MidpointRounding.AwayFromZero),
            Math.Round(longitude, DecimalPlaces, MidpointRounding.AwayFromZero));
    }
}