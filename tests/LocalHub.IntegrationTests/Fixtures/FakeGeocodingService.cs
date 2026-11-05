using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.IntegrationTests.Fixtures;

// Replaces the Nominatim adapter. It only knows the addresses below; any other address is not found.
public class FakeGeocodingService : IGeocodingService
{
    public const string KnownAddress = "Rua do Almada 10, Porto";
    public const string ReverseAddress = "Rua do Almada 10, 4050-032 Porto, Portugal";
    public const decimal KnownLatitude = 41.1579m;
    public const decimal KnownLongitude = -8.6291m;

    // North of KnownAddress on the same meridian, so the distances are exact: 2, 10 and 30 km.
    public const string AddressAt2Km = "Rua a 2 km do Almada, Porto";
    public const string AddressAt10Km = "Rua a 10 km do Almada, Porto";
    public const string AddressAt30Km = "Rua a 30 km do Almada, Porto";

    // Any address with this text simulates Nominatim being down (503).
    public const string UnavailableMarker = "[indisponivel]";

    // These coordinates simulate Nominatim being down in the reverse geocoding (503).
    public const decimal UnavailableLatitude = -89m;
    public const decimal UnavailableLongitude = 0m;

    private static readonly Coordinates Known = Coordinates.Rounded(KnownLatitude, KnownLongitude);

    private static readonly Dictionary<string, Coordinates> Addresses = new()
    {
        [KnownAddress] = Known,
        [AddressAt2Km] = Coordinates.Rounded(41.175886m, KnownLongitude),
        [AddressAt10Km] = Coordinates.Rounded(41.247832m, KnownLongitude),
        [AddressAt30Km] = Coordinates.Rounded(41.427696m, KnownLongitude),
    };

    public Task<Coordinates?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address.Contains(UnavailableMarker, StringComparison.Ordinal))
        {
            throw new GeocodingUnavailableException();
        }

        return Task.FromResult<Coordinates?>(Addresses.GetValueOrDefault(address));
    }

    public Task<string?> ReverseGeocodeAsync(Coordinates coordinates, CancellationToken cancellationToken = default)
    {
        if (coordinates == Coordinates.Rounded(UnavailableLatitude, UnavailableLongitude))
        {
            throw new GeocodingUnavailableException();
        }

        return Task.FromResult(coordinates == Known ? ReverseAddress : null);
    }
}