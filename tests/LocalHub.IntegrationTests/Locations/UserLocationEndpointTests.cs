using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using LocalHub.Infrastructure.Geocoding;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.UserLocations;

namespace LocalHub.IntegrationTests.Locations;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class UserLocationEndpointTests(LocalHubApiFactory factory)
{
    private const string Url = "/api/users/me/location";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F015-02")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SetLocation_ClientValidAddress_Returns200AndSavesAddressAndCoordinates()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await PutLocationAsync(token, new { address = FakeGeocodingService.KnownAddress });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.KnownAddress, body.GetProperty("address").GetString());
        Assert.False(body.TryGetProperty("latitude", out _));
        Assert.False(body.TryGetProperty("longitude", out _));

        var me = await GetMeAsync(token);
        Assert.Equal(FakeGeocodingService.KnownAddress, me.GetProperty("address").GetString());
        Assert.Equal((FakeGeocodingService.KnownLatitude, FakeGeocodingService.KnownLongitude),
            await ReadUserCoordinatesAsync(factory, body.GetProperty("userId").GetInt32()));
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SetLocation_AddressNotFound_Returns400OnAddressAndKeepsLocation()
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

    private async Task<HttpResponseMessage> PutLocationAsync(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> GetMeAsync(string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/auth/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}