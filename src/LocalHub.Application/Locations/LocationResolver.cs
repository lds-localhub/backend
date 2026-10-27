using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations.Dtos;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Application.Locations;

// Turns the location sent by the user (an address or a pair of coordinates) into an address and a point, using
// the geocoding service. It does not change any entity, so it can be used before loading them (E12, E16 and the
// reference location of the search of providers, E18). The sizes and the ranges were validated by the DTO; here are
// the rules that compare fields or need the geocoding service (rule R17).
public class LocationResolver(IGeocodingService geocodingService)
{
    public async Task<ResolvedLocation> ResolveAsync(SetLocationRequest request, CancellationToken cancellationToken)
    {
        bool hasAddress = !string.IsNullOrWhiteSpace(request.Address);
        bool hasCoordinates = request.Latitude is not null || request.Longitude is not null;

        if (hasAddress && hasCoordinates)
        {
            throw new ValidationException("address", "Indique a morada ou as coordenadas, mas não ambas.");
        }

        if (!hasAddress && !hasCoordinates)
        {
            throw new ValidationException("address", "Indique a morada ou as coordenadas.");
        }

        return hasAddress
            ? await FromAddressAsync(request.Address!, "address",
                "Não foi possível encontrar esta morada. Confirme-a ou indique as coordenadas.", cancellationToken)
            : await FromCoordinatesAsync(request.Latitude, request.Longitude, cancellationToken);
    }

    // Only an address, with the errors reported in the given field (E20 uses "location").
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

        return registeredLocation ?? throw new ValidationException(
            "address", "Indique uma morada ou as coordenadas da localização de referência.");
    }

    private async Task<ResolvedLocation> FromAddressAsync(
        string address, string field, string notFoundMessage, CancellationToken cancellationToken)
    {
        string trimmed = address.Trim();
        Coordinates? coordinates = await geocodingService.GeocodeAsync(trimmed, cancellationToken);
        if (coordinates is null)
        {
            throw new ValidationException(field, notFoundMessage);
        }

        return new ResolvedLocation(trimmed, coordinates);
    }

    private async Task<ResolvedLocation> FromCoordinatesAsync(
        decimal? latitude, decimal? longitude, CancellationToken cancellationToken)
    {
        Coordinates coordinates = ToPoint(latitude, longitude);

        // The address is always saved with the coordinates, so a point without an address is rejected.
        string? address = await geocodingService.ReverseGeocodeAsync(coordinates, cancellationToken);
        if (address is null)
        {
            throw new ValidationException("latitude", "Não foi possível obter uma morada para estas coordenadas.");
        }

        return new ResolvedLocation(address, coordinates);
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
