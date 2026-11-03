using LocalHub.Application.Common.Exceptions;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Application.Locations;

// Turns the address sent by the user into an address and a point, using the geocoding service. The location is always
// received as an address (E12, E14, E16, E20 and the reference location of the search of providers, E18; rule R3).
// It does not change any entity, so it can be used before loading them (rule R2). The sizes were validated by the
// DTO; here are the rules that need the geocoding service (rule R17).
public class LocationResolver
{
    private readonly IGeocodingService _geocodingService;

    public LocationResolver(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    // The errors are reported in the given field ("address", or "location" in E20) (RF-F015-03).
    public Task<ResolvedLocation> ResolveAddressAsync(string address, string field, CancellationToken cancellationToken)
    {
        return FromAddressAsync(address, field, "Não foi possível encontrar esta morada. Confirme-a.", cancellationToken);
    }

    // E18 (RF-F005-02): the reference location of a search, in this order: the coordinates, the address or the
    // registered location of the client. Nothing is saved. Unlike E16, the coordinates do not need an address, so
    // they are not sent to the geocoding service.
    public async Task<Coordinates> ResolveReferenceAsync(
        string? address,
        decimal? latitude,
        decimal? longitude,
        Coordinates? registeredLocation,
        CancellationToken cancellationToken)
    {
        if (latitude is not null || longitude is not null)
        {
            return ToPoint(latitude, longitude);
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            var location = await FromAddressAsync(address, "address",
                "Não foi possível encontrar esta morada. Confirme-a ou indique as coordenadas.", cancellationToken);
            return location.Coordinates;
        }

        if (registeredLocation is null)
        {
            throw new ValidationException(
                "address", "Indique uma morada ou as coordenadas da localização de referência.");
        }

        return registeredLocation;
    }

    private async Task<ResolvedLocation> FromAddressAsync(
        string address, string field, string notFoundMessage, CancellationToken cancellationToken)
    {
        string trimmed = address.Trim();
        Coordinates? coordinates = await _geocodingService.GeocodeAsync(trimmed, cancellationToken);
        if (coordinates is null)
        {
            throw new ValidationException(field, notFoundMessage);
        }

        return new ResolvedLocation(trimmed, coordinates);
    }

    // Both coordinates are required (RF-F015-04); their ranges were validated by the DTO.
    private static Coordinates ToPoint(decimal? latitude, decimal? longitude)
    {
        if (latitude is null)
        {
            throw new ValidationException("latitude", "Indique a latitude.");
        }

        if (longitude is null)
        {
            throw new ValidationException("longitude", "Indique a longitude.");
        }

        return Coordinates.Rounded(latitude.Value, longitude.Value);
    }
}