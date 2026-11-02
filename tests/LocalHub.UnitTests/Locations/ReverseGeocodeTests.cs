using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.UnitTests.Locations;

// E35: LocationService.ReverseGeocodeAsync only calls the geocoding service, so the database, the resolver and the
// current user are not needed (rule R3).
[Trait("Category", "Unit")]
public class ReverseGeocodeTests
{
    private const string PortoAddress = "Rua do Almada 10, 4050-032 Porto, Portugal";
    private static readonly Coordinates Porto = Coordinates.Rounded(41.1579m, -8.6291m);

    private readonly FakeGeocoding _geocoding = new();
    private readonly LocationService _service;

    public ReverseGeocodeTests()
    {
        _service = new LocationService(db: null!, resolver: null!, _geocoding, currentUser: null!);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-06")]
    public async Task ReverseGeocodeAsync_CoordenadasComMorada_DevolveAMorada()
    {
        var response = await _service.ReverseGeocodeAsync(41.1579m, -8.6291m, default);

        Assert.Equal(PortoAddress, response.Address);
        Assert.Equal(1, _geocoding.Calls);
    }

    // The coordinates are rounded to the 6 decimal places of the columns before they are sent to Nominatim.
    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task ReverseGeocodeAsync_MaisDeSeisCasasDecimais_EnviaAsCoordenadasArredondadas()
    {
        var response = await _service.ReverseGeocodeAsync(41.15790049m, -8.62909951m, default);

        Assert.Equal(PortoAddress, response.Address);
        Assert.Equal(Porto, _geocoding.LastCoordinates);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-06")]
    public async Task ReverseGeocodeAsync_SemMorada_LancaErroSemCampos()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.ReverseGeocodeAsync(0m, 0m, default));

        Assert.Equal(LocationService.AddressNotFoundMessage, exception.Message);
        Assert.Empty(exception.Errors);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task ReverseGeocodeAsync_NominatimIndisponivel_PropagaAExcecao()
    {
        _geocoding.Unavailable = true;

        await Assert.ThrowsAsync<GeocodingUnavailableException>(
            () => _service.ReverseGeocodeAsync(41.1579m, -8.6291m, default));
    }

    private sealed class FakeGeocoding : IGeocodingService
    {
        public bool Unavailable { get; set; }

        public int Calls { get; private set; }

        public Coordinates? LastCoordinates { get; private set; }

        public Task<Coordinates?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("E35 não converte moradas em coordenadas.");
        }

        public Task<string?> ReverseGeocodeAsync(Coordinates coordinates, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastCoordinates = coordinates;
            if (Unavailable)
            {
                throw new GeocodingUnavailableException();
            }

            return Task.FromResult(coordinates == Porto ? PortoAddress : null);
        }
    }
}