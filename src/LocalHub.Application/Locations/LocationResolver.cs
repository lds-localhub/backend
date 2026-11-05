using LocalHub.Application.Common.Exceptions;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Application.Locations;

public class LocationResolver
{
    private readonly IGeocodingService _geocodingService;

    public LocationResolver(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    public async Task<ResolvedLocation> ResolveAddressAsync(
        string address, string field, CancellationToken cancellationToken)
    {
        string trimmed = address.Trim();
        Coordinates? coordinates = await _geocodingService.GeocodeAsync(trimmed, cancellationToken);
        if (coordinates == null)
        {
            throw new ValidationException(field, "Não foi possível encontrar esta morada. Confirme-a.");
        }

        return new ResolvedLocation(trimmed, coordinates);
    }

    public async Task<Coordinates> ResolveReferenceAsync(
        string? address, Coordinates? registeredLocation, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            var location = await ResolveAddressAsync(address, "address", cancellationToken);
            return location.Coordinates;
        }

        if (registeredLocation == null)
        {
            throw new ValidationException("address", "Indique a morada da localização de referência.");
        }

        return registeredLocation;
    }
}