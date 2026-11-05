using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Geocoding;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;
using static LocalHub.IntegrationTests.Fixtures.UserLocations;

namespace LocalHub.IntegrationTests.Search;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class SearchEndpointsTests(LocalHubApiFactory factory)
{
    private const string Url = "/api/search/providers";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private const string KnownAddressQuery = "address=Rua%20do%20Almada%2010%2C%20Porto";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    [Trait("Requisito", "RF-F005-02")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task SearchProviders_VisitorWithAddress_Returns200WithPaginatedList()
    {
        int categoryId = await NewCategoryAsync();
        var provider = await CreateProviderAsync("Canalizações Silva", FakeGeocodingService.AddressAt2Km, 15, categoryId);

        using var response = await SearchAsync(
            $"serviceCategoryId={categoryId}&address={Uri.EscapeDataString(FakeGeocodingService.KnownAddress)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(provider.ProfessionalProfileId, item.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal(2.0m, item.GetProperty("distanceKm").GetDecimal());
    }

    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    [Trait("Requisito", "RF-F015-05")]
    [Trait("Requisito", "RF-F002-02")]
    public async Task SearchProviders_MixedProviders_ReturnsOnlyActiveProvidersWithCategoryAndLocation()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string suffix = Guid.NewGuid().ToString("N");
        int categoryId = await CreateCategoryAsync(_client, adminToken, $"Pesquisa A {suffix}");
        int otherCategoryId = await CreateCategoryAsync(_client, adminToken, $"Pesquisa B {suffix}");
        var eligible = await CreateProviderAsync("Elegível", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        var withBoth = await CreateProviderAsync(
            "Duas categorias", FakeGeocodingService.AddressAt10Km, 15, otherCategoryId, categoryId);
        await CreateProviderAsync("Outra categoria", FakeGeocodingService.AddressAt2Km, 15, otherCategoryId);
        var deactivated = await CreateProviderAsync("Conta desativada", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        await SetAccountStatusAsync(adminToken, deactivated.UserId, "deactivate");
        var withoutLocation = await CreateProviderAsync("Sem localização", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        await ClearLocationAsync(withoutLocation.UserId);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownAddressQuery}");

        var items = await ItemsAsync(response);
        Assert.Equal([eligible.ProfessionalProfileId, withBoth.ProfessionalProfileId], ProfileIds(items));
        // All the categories of the profile, ordered by name.
        Assert.Equal(
            [categoryId, otherCategoryId],
            items[1].GetProperty("serviceCategories").EnumerateArray().Select(c => c.GetProperty("serviceCategoryId").GetInt32()));
    }

    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_ProvidersAt2_10And30Km_ReturnsSortedByDistanceWithResultData()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string categoryName = $"Pesquisa {Guid.NewGuid():N}";
        int categoryId = await CreateCategoryAsync(_client, adminToken, categoryName);
        // Created out of order, so the order can only come from the distance.
        var at30 = await CreateProviderAsync("A 30 km", FakeGeocodingService.AddressAt30Km, 25, categoryId);
        var at2 = await CreateProviderAsync("A 2 km", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        var at10 = await CreateProviderAsync("A 10 km", FakeGeocodingService.AddressAt10Km, 15, categoryId);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownAddressQuery}");

        var items = await ItemsAsync(response);
        Assert.Equal([at2.ProfessionalProfileId, at10.ProfessionalProfileId, at30.ProfessionalProfileId], ProfileIds(items));
        Assert.Equal(["A 2 km", "A 10 km", "A 30 km"], items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal([2.0m, 10.0m, 30.0m], Distances(items));
        Assert.Equal([15, 15, 25], items.Select(i => i.GetProperty("serviceRadiusKm").GetInt32()));
        Assert.Equal([true, true, false], items.Select(i => i.GetProperty("withinServiceRadius").GetBoolean()));

        var category = Assert.Single(items[0].GetProperty("serviceCategories").EnumerateArray());
        Assert.Equal(categoryId, category.GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(categoryName, category.GetProperty("name").GetString());
        // Without reviews the average is null, not 0.
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("averageRating").ValueKind);
        Assert.Equal(0, items[0].GetProperty("reviewCount").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    [Trait("Requisito", "RF-F015-02")]
    public async Task SearchProviders_ClientWithSavedLocation_UsesLocationWithoutAddress()
    {
        int categoryId = await NewCategoryAsync();
        var at30 = await CreateProviderAsync("A 30 km", FakeGeocodingService.AddressAt30Km, 25, categoryId);
        var at2 = await CreateProviderAsync("A 2 km", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        string clientToken = await CreateClientWithLocationAsync(FakeGeocodingService.KnownAddress);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}", clientToken);

        var items = await ItemsAsync(response);
        Assert.Equal([at2.ProfessionalProfileId, at30.ProfessionalProfileId], ProfileIds(items));
        Assert.Equal([2.0m, 30.0m], Distances(items));
    }

    [Theory]
    [InlineData("SILVA")]
    [InlineData("  CANALIZAÇÕES  ")]
    [Trait("Requisito", "RF-F009-02")]
    public async Task SearchProviders_NameFilter_IsCaseInsensitive(string name)
    {
        int categoryId = await NewCategoryAsync();
        var silva = await CreateProviderAsync("Canalizações Silva", FakeGeocodingService.AddressAt10Km, 15, categoryId);
        await CreateProviderAsync("Jardins do Norte", FakeGeocodingService.AddressAt2Km, 15, categoryId);

        using var response = await SearchAsync(
            $"serviceCategoryId={categoryId}&{KnownAddressQuery}&name={Uri.EscapeDataString(name)}");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.Equal(silva.ProfessionalProfileId, item.GetProperty("professionalProfileId").GetInt32());
    }

    private sealed record Provider(int UserId, int ProfessionalProfileId, string Token);

    private Task<HttpResponseMessage> SearchAsync(string query, string? token = null) =>
        SendWithTokenAsync(_client, HttpMethod.Get, $"{Url}?{query}", token);

    private async Task<int> NewCategoryAsync()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        return await CreateCategoryAsync(_client, adminToken);
    }

    private async Task<Provider> CreateProviderAsync(
        string name, string address, int serviceRadiusKm, params int[] serviceCategoryIds)
    {
        string email = NewEmail();
        using var register = await RegisterAsync(_client, name, email, ValidPassword, "PROVIDER");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var account = await register.Content.ReadFromJsonAsync<JsonElement>();
        string token = await LoginAndGetTokenAsync(_client, email, ValidPassword);

        var body = ValidProfileBody(serviceCategoryIds);
        body["address"] = address;
        body["serviceRadiusKm"] = serviceRadiusKm;
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new Provider(
            account.GetProperty("userId").GetInt32(), profile.GetProperty("professionalProfileId").GetInt32(), token);
    }

    private async Task<int> GetUserIdAsync(string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/auth/me", token);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>();
        return me.GetProperty("userId").GetInt32();
    }

    private async Task<string> CreateClientWithLocationAsync(string address)
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");
        using var response = await SendJsonAsync(_client, HttpMethod.Put, "/api/users/me/location", token, new { address });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return token;
    }

    private async Task SetAccountStatusAsync(string adminToken, int userId, string action)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"/api/accounts/{userId}/{action}", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The API never creates a profile without a location, but the database scripts do.
    private async Task ClearLocationAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"User\" SET address = NULL, latitude = NULL, longitude = NULL WHERE \"userId\" = {userId}");
    }

    private static async Task<List<JsonElement>> ItemsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static List<int> ProfileIds(List<JsonElement> items) =>
        items.Select(i => i.GetProperty("professionalProfileId").GetInt32()).ToList();

    private static List<decimal> Distances(List<JsonElement> items) =>
        items.Select(i => i.GetProperty("distanceKm").GetDecimal()).ToList();

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }
}