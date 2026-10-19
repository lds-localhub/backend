using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using LocalHub.Infrastructure.Geocoding;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Locations;

// E16 PUT /api/users/me/location, with the geocoding replaced by FakeGeocodingService.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class UserLocationEndpointTests(LocalHubApiFactory factory)
{
    private const string Url = "/api/users/me/location";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("CLIENT")]
    [InlineData("PROVIDER")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SetLocation_MoradaValida_Devolve200EGuardaMoradaECoordenadas(string role)
    {
        // Only a provider with a profile can use E16 (a provider without a profile is tested in ProfilesEndpointsTests).
        string token = role == "PROVIDER"
            ? (await ProfileRequests.CreateProviderWithProfileAsync(_client)).Token
            : await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await PutLocationAsync(token, new { address = FakeGeocodingService.KnownAddress });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.KnownAddress, body.GetProperty("address").GetString());
        Assert.Equal(FakeGeocodingService.KnownLatitude, body.GetProperty("latitude").GetDecimal());
        Assert.Equal(FakeGeocodingService.KnownLongitude, body.GetProperty("longitude").GetDecimal());

        // CA7: the location is saved, so it is available to the searches by distance.
        var me = await GetMeAsync(token);
        Assert.Equal(FakeGeocodingService.KnownAddress, me.GetProperty("address").GetString());
        Assert.Equal(FakeGeocodingService.KnownLatitude, me.GetProperty("latitude").GetDecimal());
        Assert.Equal(FakeGeocodingService.KnownLongitude, me.GetProperty("longitude").GetDecimal());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SetLocation_MoradaNaoEncontrada_Devolve400NaMoradaENaoAlteraALocalizacao()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");
        using var first = await PutLocationAsync(token, new { address = FakeGeocodingService.KnownAddress });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var response = await PutLocationAsync(token, new { address = "Rua Que Não Existe 999" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(body.GetProperty("errors").TryGetProperty("address", out _));
        var me = await GetMeAsync(token);
        Assert.Equal(FakeGeocodingService.KnownAddress, me.GetProperty("address").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SetLocation_GeocodificacaoIndisponivel_Devolve503()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(
            token, new { address = $"Rua do Almada {FakeGeocodingService.UnavailableMarker}" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.6.4", Url);
        Assert.Equal(GeocodingUnavailableException.UserMessage, body.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SetLocation_CoordenadasValidas_Devolve200ComAMoradaObtida()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new
        {
            latitude = FakeGeocodingService.KnownLatitude,
            longitude = FakeGeocodingService.KnownLongitude,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.ReverseAddress, body.GetProperty("address").GetString());
        Assert.Equal(FakeGeocodingService.KnownLatitude, body.GetProperty("latitude").GetDecimal());
    }

    [Theory]
    [InlineData(90.000001, 0, "latitude")]
    [InlineData(-91, 0, "latitude")]
    [InlineData(0, 180.5, "longitude")]
    [InlineData(0, -181, "longitude")]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SetLocation_CoordenadasForaDoIntervalo_Devolve400NoCampo(
        double latitude, double longitude, string field)
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new { latitude, longitude });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SetLocation_CoordenadasSemMorada_Devolve400NaLatitude()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new { latitude = 0m, longitude = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(body.GetProperty("errors").TryGetProperty("latitude", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SetLocation_SoLatitude_Devolve400NaLongitude()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new { latitude = FakeGeocodingService.KnownLatitude });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(body.GetProperty("errors").TryGetProperty("longitude", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SetLocation_MoradaECoordenadas_Devolve400()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new
        {
            address = FakeGeocodingService.KnownAddress,
            latitude = FakeGeocodingService.KnownLatitude,
            longitude = FakeGeocodingService.KnownLongitude,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(body.GetProperty("errors").TryGetProperty("address", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SetLocation_ObjetoVazio_Devolve400()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(body.GetProperty("errors").TryGetProperty("address", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SetLocation_SemCorpo_Devolve400()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");
        using var request = new HttpRequestMessage(HttpMethod.Put, Url)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, BadRequestType, Url);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task SetLocation_Administrador_Devolve403()
    {
        using var login = await LoginAsync(_client, "ana.almeida@localhub.test", LocalHubApiFactory.SeedPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        string token = loginBody.GetProperty("token").GetString()!;

        using var response = await PutLocationAsync(token, new { address = FakeGeocodingService.KnownAddress });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.4", Url);
    }

    // CA5: the API used by the tests never reaches the real Nominatim.
    [Fact]
    public async Task Testes_NaoUsamONominatimReal()
    {
        Assert.IsType<FakeGeocodingService>(factory.Services.GetRequiredService<IGeocodingService>());

        using var client = factory.Services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(GeocodingServiceCollectionExtensions.HttpClientName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(new Uri("search?q=Porto", UriKind.Relative)));
    }

    private async Task<HttpResponseMessage> PutLocationAsync(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> GetMeAsync(string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/users/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}