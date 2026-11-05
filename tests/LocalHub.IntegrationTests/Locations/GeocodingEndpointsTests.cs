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
    public async Task Reverse_CoordinatesWithAddress_Returns200WithAddressWithoutSavingData()
    {
        var before = await CountUsersWithLocationAsync();

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, Url(FakeGeocodingService.KnownLatitude, FakeGeocodingService.KnownLongitude), token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.ReverseAddress, body.GetProperty("address").GetString());
        Assert.Equal(before, await CountUsersWithLocationAsync());
    }

    [Theory]
    [InlineData("longitude=-8.6291", "latitude")]
    [InlineData("latitude=90.5&longitude=-8.6291", "latitude")]
    [InlineData("latitude=41.1579&longitude=180.5", "longitude")]
    [Trait("Requisito", "RF-F015-04")]
    public async Task Reverse_MissingOrOutOfRangeCoordinates_Returns400WithFieldError(string query, string field)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, $"{ReverseUrl}?{query}", token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, ReverseUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
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