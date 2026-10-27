using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Infrastructure.Geocoding;

public interface IGeocodingService
{
    Task<Coordinates?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

    Task<string?> ReverseGeocodeAsync(Coordinates coordinates, CancellationToken cancellationToken = default);
}