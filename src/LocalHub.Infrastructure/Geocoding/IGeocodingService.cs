using LocalHub.Domain.Locations;

namespace LocalHub.Infrastructure.Geocoding;

public interface IGeocodingService
{
    Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

    Task<string?> ReverseGeocodeAsync(GeoPoint coordinates, CancellationToken cancellationToken = default);
}