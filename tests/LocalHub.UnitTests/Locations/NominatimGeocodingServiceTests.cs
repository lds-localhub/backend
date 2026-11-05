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
    public async Task GeocodeAsync_AddressFound_ReturnsCoordinates()
    {
        using StubHandler handler = new(HttpStatusCode.OK, """[{"lat":"41.1579000","lon":"-8.6291000","display_name":"Praça da Liberdade, Porto"}]""");
        NominatimGeocodingService service = CreateService(handler);

        Coordinates? result = await service.GeocodeAsync("Praça da Liberdade, Porto");

        Assert.NotNull(result);
        Assert.Equal(41.1579m, result.Latitude);
        Assert.Equal(-8.6291m, result.Longitude);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-03")]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GeocodeAsync_NominatimError_ThrowsGeocodingUnavailable(HttpStatusCode status)
    {
        using StubHandler handler = new(status, string.Empty);
        NominatimGeocodingService service = CreateService(handler);

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => service.GeocodeAsync("Porto"));
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

            if (_exception != null)
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