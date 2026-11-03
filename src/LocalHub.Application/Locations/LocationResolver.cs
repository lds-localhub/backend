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
    public async Task<ResolvedLocation> ResolveAddressAsync(
        string address, string field, CancellationToken cancellationToken)
    {
        string trimmed = address.Trim();
        Coordinates? coordinates = await _geocodingService.GeocodeAsync(trimmed, cancellationToken);
        if (coordinates is null)
        {
            throw new ValidationException(field, "Não foi possível encontrar esta morada. Confirme-a.");
        }

        return new ResolvedLocation(trimmed, coordinates);
    }

    // E18 (RF-F005-02): the reference location of a search, in this order: the address or the registered location of
    // the client. Nothing is saved. In the mobile application the address can be filled from the position of the
    // device with E35 (RF-F015-06).
    public async Task<Coordinates> ResolveReferenceAsync(
        string? address, Coordinates? registeredLocation, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            var location = await ResolveAddressAsync(address, "address", cancellationToken);
            return location.Coordinates;
        }

        if (registeredLocation is null)
        {
            throw new ValidationException("address", "Indique a morada da localização de referência.");
        }

        return registeredLocation;
    }
}