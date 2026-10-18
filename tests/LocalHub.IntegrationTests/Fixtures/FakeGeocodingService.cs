using LocalHub.Domain.Locations;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.IntegrationTests.Fixtures;

// Replaces the Nominatim adapter in the integration tests (I043: no test calls the real Nominatim).
// It only knows one address; any other address is "not found".
public class FakeGeocodingService : IGeocodingService
{
    public const string KnownAddress = "Rua do Almada 10, Porto";
    public const string ReverseAddress = "Rua do Almada 10, 4050-032 Porto, Portugal";
    public const decimal KnownLatitude = 41.1579m;
    public const decimal KnownLongitude = -8.6291m;

    // Any address with this text simulates Nominatim being down (503).
    public const string UnavailableMarker = "[indisponivel]";

    private static readonly GeoPoint Known = GeoPoint.Create(KnownLatitude, KnownLongitude);

    public Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address.Contains(UnavailableMarker, StringComparison.Ordinal))
        {
            throw new GeocodingUnavailableException();
        }

        return Task.FromResult<GeoPoint?>(address == KnownAddress ? Known : null);
    }

    public Task<string?> ReverseGeocodeAsync(GeoPoint coordinates, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(coordinates == Known ? ReverseAddress : null);
    }
}

// Primary handler of the "Nominatim" HttpClient in the tests: if anything still uses the real adapter, the test fails.
public class FailIfCalledHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException($"Os testes não podem chamar o Nominatim real ({request.RequestUri}).");
    }
}