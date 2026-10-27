namespace LocalHub.Infrastructure.Geocoding;

// A point on the Earth (WGS 84), as returned by the geocoding service or received in a request (E16, E18). It is only a
// helper type (it is not saved): User and Service keep the coordinates in their properties Latitude and Longitude.
public record Coordinates(decimal Latitude, decimal Longitude)
{
    // Same number of decimal places as the columns latitude numeric(8,6) and longitude numeric(9,6).
    public const int DecimalPlaces = 6;

    // Rounds to 6 decimal places, so the coordinates returned now are the same that will be read from the database.
    public static Coordinates Rounded(decimal latitude, decimal longitude)
    {
        return new Coordinates(
            Math.Round(latitude, DecimalPlaces, MidpointRounding.AwayFromZero),
            Math.Round(longitude, DecimalPlaces, MidpointRounding.AwayFromZero));
    }
}