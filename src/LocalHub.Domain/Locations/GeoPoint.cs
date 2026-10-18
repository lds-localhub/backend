using LocalHub.Domain.Common;

namespace LocalHub.Domain.Locations;

/// <summary>
/// Represents a point on the Earth (WGS 84), with 6 decimal places like the columns latitude and longitude of "User".
/// </summary>
public readonly record struct GeoPoint
{
    public const int DecimalPlaces = 6;

    // Private: a point is always created with Create, so the ranges below are always checked.
    private GeoPoint(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    public static GeoPoint Create(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m)
        {
            throw new DomainException(DomainErrorKind.Validation, "A latitude tem de estar entre -90 e 90.", "latitude");
        }

        if (longitude is < -180m or > 180m)
        {
            throw new DomainException(DomainErrorKind.Validation, "A longitude tem de estar entre -180 e 180.", "longitude");
        }

        return new GeoPoint(
            Math.Round(latitude, DecimalPlaces, MidpointRounding.AwayFromZero),
            Math.Round(longitude, DecimalPlaces, MidpointRounding.AwayFromZero));
    }
}