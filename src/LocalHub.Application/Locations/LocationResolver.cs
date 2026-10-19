using LocalHub.Application.Locations.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Application.Locations;

// Turns the location sent by the user (an address or a pair of coordinates) into an address and a point, using
// the geocoding service. It does not change any entity, so it can be used before loading them (E16 and, later,
// the features that search by distance).
public class LocationResolver(IGeocodingService geocodingService)
{
    public async Task<ResolvedLocation> ResolveAsync(SetLocationRequest request, CancellationToken cancellationToken)
    {
        bool hasAddress = !string.IsNullOrWhiteSpace(request.Address);
        bool hasCoordinates = request.Latitude is not null || request.Longitude is not null;

        if (hasAddress && hasCoordinates)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Indique a morada ou as coordenadas, mas não ambas.", "address");
        }

        if (!hasAddress && !hasCoordinates)
        {
            throw new DomainException(DomainErrorKind.Validation, "Indique a morada ou as coordenadas.", "address");
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

    private async Task<ResolvedLocation> FromAddressAsync(
        string address, string field, string notFoundMessage, CancellationToken cancellationToken)
    {
        string trimmed = address.Trim();
        if (trimmed.Length > User.AddressMaxLength)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"A morada tem no máximo {User.AddressMaxLength} caracteres.", field);
        }

        GeoPoint? coordinates = await geocodingService.GeocodeAsync(trimmed, cancellationToken);
        if (coordinates is null)
        {
            throw new DomainException(DomainErrorKind.Validation, notFoundMessage, field);
        }

        return new ResolvedLocation(trimmed, coordinates.Value);
    }

    private async Task<ResolvedLocation> FromCoordinatesAsync(
        decimal? latitude, decimal? longitude, CancellationToken cancellationToken)
    {
        if (latitude is null)
        {
            throw new DomainException(DomainErrorKind.Validation, "Indique a latitude.", "latitude");
        }

        if (longitude is null)
        {
            throw new DomainException(DomainErrorKind.Validation, "Indique a longitude.", "longitude");
        }

        GeoPoint coordinates = GeoPoint.Create(latitude.Value, longitude.Value);

        // The address is always saved with the coordinates, so a point without an address is rejected.
        string? address = await geocodingService.ReverseGeocodeAsync(coordinates, cancellationToken);
        if (address is null)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Não foi possível obter uma morada para estas coordenadas.", "latitude");
        }

        return new ResolvedLocation(address, coordinates);
    }
}