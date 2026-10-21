using LocalHub.Domain.Locations;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.IntegrationTests.Fixtures;

// Replaces the Nominatim adapter in the integration tests (I043: no test calls the real Nominatim).
// It only knows the addresses below; any other address is "not found".
public class FakeGeocodingService : IGeocodingService
{
    public const string KnownAddress = "Rua do Almada 10, Porto";
    public const string ReverseAddress = "Rua do Almada 10, 4050-032 Porto, Portugal";
    public const decimal KnownLatitude = 41.1579m;
    public const decimal KnownLongitude = -8.6291m;

    // Fictitious addresses north of KnownAddress, on the same meridian, for the searches by distance (F005). On a
    // meridian the distance is R · Δφ, so with R = 6371 km and 6 decimal places they are at 2.0, 10.0 and 30.0 km.
    public const string AddressAt2Km = "Rua a 2 km do Almada, Porto";
    public const string AddressAt10Km = "Rua a 10 km do Almada, Porto";
    public const string AddressAt30Km = "Rua a 30 km do Almada, Porto";

    // Any address with this text simulates Nominatim being down (503).
    public const string UnavailableMarker = "[indisponivel]";

    private static readonly GeoPoint Known = GeoPoint.Create(KnownLatitude, KnownLongitude);

    private static readonly Dictionary<string, GeoPoint> Addresses = new()
    {
        [KnownAddress] = Known,
        [AddressAt2Km] = GeoPoint.Create(41.175886m, KnownLongitude),
        [AddressAt10Km] = GeoPoint.Create(41.247832m, KnownLongitude),
        [AddressAt30Km] = GeoPoint.Create(41.427696m, KnownLongitude),
    };

    public Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address.Contains(UnavailableMarker, StringComparison.Ordinal))
        {
            throw new GeocodingUnavailableException();
        }

        return Task.FromResult<GeoPoint?>(Addresses.TryGetValue(address, out var point) ? point : null);
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