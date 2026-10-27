using System.Net;
using System.Text;

using LocalHub.Infrastructure.Geocoding;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class NominatimGeocodingServiceTests
{
    private const string UserAgent = "LocalHub-Testes/1.0 (LDS G14)";

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task GeocodeAsync_MoradaEncontrada_DevolveCoordenadas()
    {
        using StubHandler handler = new(HttpStatusCode.OK, """[{"lat":"41.1579000","lon":"-8.6291000","display_name":"Praça da Liberdade, Porto"}]""");
        NominatimGeocodingService service = CreateService(handler);

        Coordinates? result = await service.GeocodeAsync("Praça da Liberdade, Porto");

        Assert.NotNull(result);
        Assert.Equal(41.1579m, result.Latitude);
        Assert.Equal(-8.6291m, result.Longitude);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task GeocodeAsync_MoradaNaoEncontrada_DevolveNull()
    {
        using StubHandler handler = new(HttpStatusCode.OK, "[]");
        NominatimGeocodingService service = CreateService(handler);

        Coordinates? result = await service.GeocodeAsync("Rua Inexistente 99999");

        Assert.Null(result);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task GeocodeAsync_PedidoEnviado_IdentificaAAplicacaoEFiltraPortugal()
    {
        using StubHandler handler = new(HttpStatusCode.OK, "[]");
        NominatimGeocodingService service = CreateService(handler);

        await service.GeocodeAsync("Rua de Santa Catarina, Porto");

        Uri requestUri = Assert.Single(handler.RequestUris);
        Assert.Contains("LocalHub-Testes", Assert.Single(handler.UserAgents), StringComparison.Ordinal);
        Assert.Equal("/search", requestUri.AbsolutePath);
        Assert.Contains("countrycodes=pt", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("format=jsonv2", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("limit=1", requestUri.Query, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-03")]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GeocodeAsync_NominatimComErro_LancaGeocodingUnavailable(HttpStatusCode status)
    {
        using StubHandler handler = new(status, string.Empty);
        NominatimGeocodingService service = CreateService(handler);

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => service.GeocodeAsync("Porto"));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task GeocodeAsync_FalhaDeLigacao_LancaGeocodingUnavailable()
    {
        using StubHandler handler = new(new HttpRequestException("sem rede"));
        NominatimGeocodingService service = CreateService(handler);

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => service.GeocodeAsync("Porto"));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task GeocodeAsync_TempoLimiteExcedido_LancaGeocodingUnavailable()
    {
        using StubHandler handler = new(new TaskCanceledException("tempo limite"));
        NominatimGeocodingService service = CreateService(handler);

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => service.GeocodeAsync("Porto"));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task ReverseGeocodeAsync_CoordenadasComMorada_DevolveAMorada()
    {
        using StubHandler handler = new(HttpStatusCode.OK, """{"lat":"41.1579","lon":"-8.6291","display_name":"Praça da Liberdade, Porto"}""");
        NominatimGeocodingService service = CreateService(handler);

        string? address = await service.ReverseGeocodeAsync(Coordinates.Rounded(41.1579m, -8.6291m));

        Assert.Equal("Praça da Liberdade, Porto", address);
        Uri requestUri = Assert.Single(handler.RequestUris);
        Assert.Equal("/reverse", requestUri.AbsolutePath);
        Assert.Contains("lat=41.1579", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("lon=-8.6291", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task ReverseGeocodeAsync_SemMorada_DevolveNull()
    {
        using StubHandler handler = new(HttpStatusCode.OK, """{"error":"Unable to geocode"}""");
        NominatimGeocodingService service = CreateService(handler);

        string? address = await service.ReverseGeocodeAsync(Coordinates.Rounded(0m, 0m));

        Assert.Null(address);
    }

    private static NominatimGeocodingService CreateService(StubHandler handler)
    {
        IOptions<NominatimOptions> options = Options.Create(new NominatimOptions
        {
            BaseUrl = new Uri("https://nominatim.test/"),
            UserAgent = UserAgent,
        });

        HttpClient httpClient = new(handler, disposeHandler: false) { BaseAddress = options.Value.BaseUrl };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        NominatimRateLimiter rateLimiter = new(options, new FakeTimeProvider());

        return new NominatimGeocodingService(httpClient, rateLimiter, options, NullLogger<NominatimGeocodingService>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubHandler(Exception exception)
        {
            _exception = exception;
        }

        public List<Uri> RequestUris { get; } = [];

        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            UserAgents.Add(request.Headers.UserAgent.ToString());

            if (_exception is not null)
            {
                return Task.FromException<HttpResponseMessage>(_exception);
            }

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}