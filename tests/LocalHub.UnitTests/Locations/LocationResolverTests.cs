using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class LocationResolverTests
{
    private const string KnownAddress = "Rua do Almada 10, Porto";
    private static readonly Coordinates Porto = Coordinates.Rounded(41.1579m, -8.6291m);

    // Registered location of a client and coordinates given in a search (ResolveReferenceAsync), both different from
    // Porto, so the tests can tell which one was used.
    private static readonly Coordinates Braga = Coordinates.Rounded(41.5454m, -8.4265m);
    private static readonly Coordinates Coimbra = Coordinates.Rounded(40.2033m, -8.4103m);

    private readonly FakeGeocoding _geocoding = new();
    private readonly LocationResolver _resolver;

    public LocationResolverTests()
    {
        _resolver = new LocationResolver(_geocoding);
    }

    // ---------- ResolveAddressAsync: the location received as an address (E12, E14, E16, E20) ----------

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task ResolveAddressAsync_MoradaEncontrada_DevolveMoradaECoordenadas()
    {
        var location = await _resolver.ResolveAddressAsync($"  {KnownAddress} ", "address", default);

        Assert.Equal(KnownAddress, location.Address);
        Assert.Equal(Porto, location.Coordinates);
        Assert.Equal(KnownAddress, _geocoding.LastAddress);
    }

    [Theory]
    [InlineData("address")]
    [InlineData("location")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task ResolveAddressAsync_MoradaNaoEncontrada_LancaErroNoCampoIndicado(string field)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _resolver.ResolveAddressAsync("Rua Que Não Existe", field, default));

        Assert.Equal(field, Assert.Single(exception.Errors).Key);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task ResolveAddressAsync_GeocodificadorIndisponivel_PropagaAExcecao()
    {
        _geocoding.Unavailable = true;

        await Assert.ThrowsAsync<GeocodingUnavailableException>(
            () => _resolver.ResolveAddressAsync(KnownAddress, "address", default));
    }

    // ---------- ResolveReferenceAsync: reference location of the search of providers (E18) ----------

    // The coordinates come first, before the address and the registered location, and need no address.
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    public async Task ResolveReferenceAsync_Coordenadas_UsaAsCoordenadasSemChamarOGeocodificador()
    {
        Coordinates reference = await _resolver.ResolveReferenceAsync(
            KnownAddress, Coimbra.Latitude, Coimbra.Longitude, Braga, default);

        Assert.Equal(Coimbra, reference);
        Assert.Equal(0, _geocoding.Calls);
    }

    // RF-F005-02, rule 2: an address given in the search replaces the registered location.
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    public async Task ResolveReferenceAsync_Morada_UsaAMoradaConvertidaEmVezDaLocalizacaoRegistada()
    {
        Coordinates reference = await _resolver.ResolveReferenceAsync($"  {KnownAddress} ", null, null, Braga, default);

        Assert.Equal(Porto, reference);
        Assert.Equal(KnownAddress, _geocoding.LastAddress);
    }

    // RF-F005-02, rule 1.
    [Theory]
    [Trait("Requisito", "RF-F005-02")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ResolveReferenceAsync_SemMoradaNemCoordenadas_UsaALocalizacaoRegistada(string? address)
    {
        Coordinates reference = await _resolver.ResolveReferenceAsync(address, null, null, Braga, default);

        Assert.Equal(Braga, reference);
        Assert.Equal(0, _geocoding.Calls);
    }

    // RF-F005-01, rule 5: without a reference location the search is not done.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    [Trait("Requisito", "RF-F005-02")]
    public async Task ResolveReferenceAsync_SemNenhumaLocalizacao_LancaErroNaMorada()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _resolver.ResolveReferenceAsync(null, null, null, null, default));

        Assert.Equal("address", Assert.Single(exception.Errors).Key);
    }

    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task ResolveReferenceAsync_MoradaNaoEncontrada_LancaErroNaMorada()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _resolver.ResolveReferenceAsync("Rua Que Não Existe", null, null, Braga, default));

        Assert.Equal("address", Assert.Single(exception.Errors).Key);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-04")]
    [InlineData(41.1579, null, "longitude")]
    [InlineData(null, -8.6291, "latitude")]
    public async Task ResolveReferenceAsync_SoUmaCoordenada_LancaErroNoCampoSemChamarOGeocodificador(
        double? latitude, double? longitude, string field)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _resolver.ResolveReferenceAsync(
            KnownAddress, (decimal?)latitude, (decimal?)longitude, Braga, default));

        Assert.Equal(field, Assert.Single(exception.Errors).Key);
        Assert.Equal(0, _geocoding.Calls);
    }

    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    public async Task ResolveReferenceAsync_GeocodificadorIndisponivel_PropagaAExcecao()
    {
        _geocoding.Unavailable = true;

        await Assert.ThrowsAsync<GeocodingUnavailableException>(
            () => _resolver.ResolveReferenceAsync(KnownAddress, null, null, Braga, default));
    }

    private sealed class FakeGeocoding : IGeocodingService
    {
        public bool Unavailable { get; set; }

        public int Calls { get; private set; }

        public string? LastAddress { get; private set; }

        public Task<Coordinates?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastAddress = address;
            if (Unavailable)
            {
                throw new GeocodingUnavailableException();
            }

            return Task.FromResult<Coordinates?>(address == KnownAddress ? Porto : null);
        }

        public Task<string?> ReverseGeocodeAsync(Coordinates coordinates, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(coordinates == Porto ? KnownAddress : null);
        }
    }
}