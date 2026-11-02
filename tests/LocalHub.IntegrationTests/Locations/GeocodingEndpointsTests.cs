using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Locations;

// E35 (address of the position of the device). Public: no request of this class sends a token.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class GeocodingEndpointsTests(LocalHubApiFactory factory)
{
    private const string ReverseUrl = "/api/geocoding/reverse";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string UnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F015-06")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task Reverse_CoordenadasComMorada_Devolve200ComAMoradaSemGravarDados()
    {
        var before = await CountUsersWithLocationAsync();

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, Url(FakeGeocodingService.KnownLatitude, FakeGeocodingService.KnownLongitude), token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.ReverseAddress, body.GetProperty("address").GetString());
        Assert.Equal(before, await CountUsersWithLocationAsync());
    }

    // A client that is logged in gets the same answer, and its registered location does not change (RF-F015-06).
    [Fact]
    [Trait("Requisito", "RF-F015-06")]
    public async Task Reverse_ComSessaoDeCliente_NaoAlteraALocalizacaoRegistada()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email, "CLIENT");

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, Url(FakeGeocodingService.KnownLatitude, FakeGeocodingService.KnownLongitude), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.Null(user.Address);
        Assert.Null(user.Latitude);
        Assert.Null(user.Longitude);
    }

    [Theory]
    [InlineData("longitude=-8.6291", "latitude")]
    [InlineData("latitude=41.1579", "longitude")]
    [InlineData("latitude=90.5&longitude=-8.6291", "latitude")]
    [InlineData("latitude=-90.5&longitude=-8.6291", "latitude")]
    [InlineData("latitude=41.1579&longitude=180.5", "longitude")]
    [InlineData("latitude=41.1579&longitude=-180.5", "longitude")]
    [InlineData("latitude=abc&longitude=-8.6291", "latitude")]
    [Trait("Requisito", "RF-F015-04")]
    public async Task Reverse_CoordenadasEmFaltaOuForaDosIntervalos_Devolve400ComErroNoCampo(string query, string field)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, $"{ReverseUrl}?{query}", token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, ReverseUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-04")]
    public async Task Reverse_SemParametros_Devolve400ComOsDoisCampos()
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReverseUrl, token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await AssertProblemAsync(response, BadRequestType, ReverseUrl)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("latitude", out _));
        Assert.True(errors.TryGetProperty("longitude", out _));
    }

    // The limits of the ranges are valid coordinates (RF-F015-04): they reach the geocoding service, which has no
    // address for them.
    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [Trait("Requisito", "RF-F015-04")]
    public async Task Reverse_LimitesDosIntervalos_NaoDevolveErroNosCampos(decimal latitude, decimal longitude)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, Url(latitude, longitude), token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, ReverseUrl);
        Assert.False(body.TryGetProperty("errors", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-06")]
    public async Task Reverse_NominatimSemMorada_Devolve400ComIndicacaoNoDetail()
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, Url(0m, 0m), token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, ReverseUrl);
        Assert.Equal(
            "Não foi possível obter a morada desta posição. Introduza o endereço.",
            body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task Reverse_NominatimIndisponivel_Devolve503()
    {
        using var response = await SendWithTokenAsync(
            _client,
            HttpMethod.Get,
            Url(FakeGeocodingService.UnavailableLatitude, FakeGeocodingService.UnavailableLongitude),
            token: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemAsync(response, UnavailableType, ReverseUrl);
    }

    private static string Url(decimal latitude, decimal longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{ReverseUrl}?latitude={latitude}&longitude={longitude}");

    private async Task<int> CountUsersWithLocationAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.CountAsync(u => u.Latitude != null);
    }
}