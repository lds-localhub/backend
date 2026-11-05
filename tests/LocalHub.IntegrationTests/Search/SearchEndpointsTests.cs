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

// F005: E18 GET /api/search/providers, with the geocoding replaced by FakeGeocodingService. Each test uses its own
// category, so it only finds the providers that it creates. In most tests the reference location is KnownAddress and
// the providers are at 2, 10 and 30 km to the north of it (FakeGeocodingService).
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class SearchEndpointsTests(LocalHubApiFactory factory)
{
    private const string Url = "/api/search/providers";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    // The coordinates of FakeGeocodingService.KnownAddress.
    // FakeGeocodingService.KnownAddress, "Rua do Almada 10, Porto".
    private const string KnownAddressQuery = "address=Rua%20do%20Almada%2010%2C%20Porto";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- Who can search and who is found ----------

    // CA1 (RF-F005-01, rule 1) and RF-F005-02, criterion 2: a visitor searches with an address.
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

    // CA2 (RF-F005-01, rule 3 and criteria 2 and 3; RF-F015-05, criterion 2; RF-F002-02, rule 7). A provider without a
    // profile has no categories, so it is never a result.
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
        // All the categories of the profile, not only the one of the search, ordered by name.
        Assert.Equal(
            [categoryId, otherCategoryId],
            items[1].GetProperty("serviceCategories").EnumerateArray().Select(c => c.GetProperty("serviceCategoryId").GetInt32()));
    }

    // CA3 (RF-F005-01, rules 4 and 6, criteria 4 and 6) and CA8: on a meridian the distances are the known values R · Δφ.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_ProvidersAt2_10And30Km_ReturnsSortedByDistanceWithResultData()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string categoryName = $"Pesquisa {Guid.NewGuid():N}";
        int categoryId = await CreateCategoryAsync(_client, adminToken, categoryName);
        // Created out of order, so the order of the results can only come from the distance.
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
        // Without reviews the average rating is null, not 0 (RF-F012-02, rule 3), and reviewCount is 0.
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("averageRating").ValueKind);
        Assert.Equal(0, items[0].GetProperty("reviewCount").GetInt32());
    }

    // ---------- Reference location (RF-F005-02) ----------

    // CA5 (RF-F005-02, rule 1 and criterion 1).
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

    // ---------- Name filter (RF-F009-02, rule 2) ----------

    // CA7 (RF-F009-02, criterion 2): part of the name, without telling upper and lower case apart.
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

    // ---------- Helpers ----------

    private sealed record Provider(int UserId, int ProfessionalProfileId, string Token);

    private Task<HttpResponseMessage> SearchAsync(string query, string? token = null) =>
        SendWithTokenAsync(_client, HttpMethod.Get, $"{Url}?{query}", token);

    private async Task<int> NewCategoryAsync()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        return await CreateCategoryAsync(_client, adminToken);
    }

    // A provider with a professional profile (E01, E02 and E12), at the address given, known by FakeGeocodingService.
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

    // action: "deactivate" (E06) or "activate" (E07).
    private async Task SetAccountStatusAsync(string adminToken, int userId, string action)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"/api/accounts/{userId}/{action}", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The API does not allow a provider with a profile and without a location (E12 requires the address), but the
    // reference data of the database scripts has one (database/README.md, section 4.3).
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