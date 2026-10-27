namespace LocalHub.Domain.Locations;

/// <summary>
/// Represents a point on the Earth (WGS 84), with 6 decimal places like the columns latitude and longitude.
/// It is only a helper type (it is not saved): the coordinates come from the geocoding service or from the DTO of
/// the request, which validates their ranges (rule R17).
/// </summary>
public readonly record struct GeoPoint
{
    public const int DecimalPlaces = 6;

    // Private: a point is always created with Create, so the coordinates are always rounded.
    private GeoPoint(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    public static GeoPoint Create(decimal latitude, decimal longitude)
    {
        return new GeoPoint(
            Math.Round(latitude, DecimalPlaces, MidpointRounding.AwayFromZero),
            Math.Round(longitude, DecimalPlaces, MidpointRounding.AwayFromZero));
    }
}
