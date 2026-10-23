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
    private const string KnownCoordinates = "latitude=41.1579&longitude=-8.6291";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- Who can search and who is found ----------

    // CA1 (RF-F005-01, rule 1) and RF-F005-02, criterion 2: a visitor searches with an address.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    [Trait("Requisito", "RF-F005-02")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task SearchProviders_VisitanteComMorada_Devolve200ComAListaPaginada()
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
    public async Task SearchProviders_SoDevolvePrestadoresAtivosComACategoriaELocalizacao()
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

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}");

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
    public async Task SearchProviders_PrestadoresA2_10E30Km_DevolveOrdenadosPorDistanciaComOsDadosDeCadaResultado()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string categoryName = $"Pesquisa {Guid.NewGuid():N}";
        int categoryId = await CreateCategoryAsync(_client, adminToken, categoryName);
        // Created out of order, so the order of the results can only come from the distance.
        var at30 = await CreateProviderAsync("A 30 km", FakeGeocodingService.AddressAt30Km, 25, categoryId);
        var at2 = await CreateProviderAsync("A 2 km", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        var at10 = await CreateProviderAsync("A 10 km", FakeGeocodingService.AddressAt10Km, 15, categoryId);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}");

        var items = await ItemsAsync(response);
        Assert.Equal([at2.ProfessionalProfileId, at10.ProfessionalProfileId, at30.ProfessionalProfileId], ProfileIds(items));
        Assert.Equal(["A 2 km", "A 10 km", "A 30 km"], items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal([2.0m, 10.0m, 30.0m], Distances(items));
        Assert.Equal([15, 15, 25], items.Select(i => i.GetProperty("serviceRadiusKm").GetInt32()));
        Assert.Equal([true, true, false], items.Select(i => i.GetProperty("withinServiceRadius").GetBoolean()));

        var category = Assert.Single(items[0].GetProperty("serviceCategories").EnumerateArray());
        Assert.Equal(categoryId, category.GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(categoryName, category.GetProperty("name").GetString());
        // Without reviews the average rating is null, not 0 (RF-F012-02, rule 3).
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("averageRating").ValueKind);
    }

    // CA4 (RF-F005-01, rule 7 and criterion 7): the service radius does not limit the results.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_PrestadorA30KmComRaioDe25_EApresentadoForaDoRaio()
    {
        int categoryId = await NewCategoryAsync();
        var provider = await CreateProviderAsync("A 30 km", FakeGeocodingService.AddressAt30Km, 25, categoryId);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.Equal(provider.ProfessionalProfileId, item.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal(30.0m, item.GetProperty("distanceKm").GetDecimal());
        Assert.Equal(25, item.GetProperty("serviceRadiusKm").GetInt32());
        Assert.False(item.GetProperty("withinServiceRadius").GetBoolean());
    }

    // RF-F015-05, criterion 1: after the provider changes its location (E16), the search uses the new one.
    [Fact]
    [Trait("Requisito", "RF-F015-05")]
    public async Task SearchProviders_PrestadorAlteraALocalizacao_UsaANovaLocalizacao()
    {
        int categoryId = await NewCategoryAsync();
        var provider = await CreateProviderAsync("Muda de morada", FakeGeocodingService.AddressAt30Km, 25, categoryId);
        using var move = await SendJsonAsync(
            _client, HttpMethod.Put, "/api/users/me/location", provider.Token, new { address = FakeGeocodingService.AddressAt2Km });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.Equal(2.0m, item.GetProperty("distanceKm").GetDecimal());
        Assert.True(item.GetProperty("withinServiceRadius").GetBoolean());
    }

    // ---------- Reference location (RF-F005-02) ----------

    // CA5 (RF-F005-02, rule 1 and criterion 1).
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    [Trait("Requisito", "RF-F015-02")]
    public async Task SearchProviders_ClienteComLocalizacaoRegistada_UsaALocalizacaoSemIndicarMorada()
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

    // CA5 (RF-F005-02, rules 2 and 3 and criterion 3): the address replaces the registered location only in this search.
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    public async Task SearchProviders_ClienteIndicaOutraMorada_UsaAMoradaENaoAlteraALocalizacaoRegistada()
    {
        int categoryId = await NewCategoryAsync();
        var at30 = await CreateProviderAsync("A 30 km", FakeGeocodingService.AddressAt30Km, 25, categoryId);
        var at2 = await CreateProviderAsync("A 2 km", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        string clientToken = await CreateClientWithLocationAsync(FakeGeocodingService.KnownAddress);

        using var response = await SearchAsync(
            $"serviceCategoryId={categoryId}&address={Uri.EscapeDataString(FakeGeocodingService.AddressAt30Km)}", clientToken);

        var items = await ItemsAsync(response);
        Assert.Equal([at30.ProfessionalProfileId, at2.ProfessionalProfileId], ProfileIds(items));
        Assert.Equal([0.0m, 28.0m], Distances(items));

        using var meResponse = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/users/me", clientToken);
        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.KnownAddress, me.GetProperty("address").GetString());
        Assert.Equal(FakeGeocodingService.KnownLatitude, me.GetProperty("latitude").GetDecimal());
        Assert.Equal(FakeGeocodingService.KnownLongitude, me.GetProperty("longitude").GetDecimal());
    }

    // The coordinates come first (API documentation, E18), so the address is not sent to the Nominatim: here it would
    // answer 503.
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    public async Task SearchProviders_CoordenadasEMorada_UsaAsCoordenadas()
    {
        int categoryId = await NewCategoryAsync();
        await CreateProviderAsync("A 2 km", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        string address = Uri.EscapeDataString($"Rua do Almada {FakeGeocodingService.UnavailableMarker}");

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}&address={address}");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.Equal(2.0m, item.GetProperty("distanceKm").GetDecimal());
    }

    // CA6 (RF-F005-01, rule 5 and criterion 5). Only the registered location of a client is used by default: a provider
    // with a location (from E12) also has to indicate one.
    [Theory]
    [InlineData(null)]
    [InlineData("CLIENT")]
    [InlineData("PROVIDER")]
    [Trait("Requisito", "RF-F005-01")]
    [Trait("Requisito", "RF-F005-02")]
    public async Task SearchProviders_SemLocalizacaoDeReferencia_Devolve400NaMorada(string? role)
    {
        int categoryId = await NewCategoryAsync();
        string? token = role switch
        {
            "CLIENT" => await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT"),
            "PROVIDER" => (await CreateProviderWithProfileAsync(_client)).Token,
            _ => null,
        };

        using var response = await SearchAsync($"serviceCategoryId={categoryId}", token);

        await AssertFieldErrorAsync(response, "address");
    }

    // CA6 (RF-F015-03, criterion 2).
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task SearchProviders_MoradaNaoReconhecida_Devolve400NaMorada()
    {
        int categoryId = await NewCategoryAsync();

        using var response = await SearchAsync(
            $"serviceCategoryId={categoryId}&address={Uri.EscapeDataString("Rua Que Não Existe 999")}");

        await AssertFieldErrorAsync(response, "address");
    }

    // CA6: the Nominatim is down.
    [Fact]
    [Trait("Requisito", "RF-F005-02")]
    public async Task SearchProviders_GeocodificacaoIndisponivel_Devolve503()
    {
        int categoryId = await NewCategoryAsync();
        string address = Uri.EscapeDataString($"Rua do Almada {FakeGeocodingService.UnavailableMarker}");

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&address={address}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.6.4", Url);
        Assert.Equal(GeocodingUnavailableException.UserMessage, body.GetProperty("detail").GetString());
    }

    // CA6 (RF-F015-04, criterion 2): coordinates outside the valid ranges, missing or not a number.
    [Theory]
    [InlineData("latitude=95&longitude=10", "latitude")]
    [InlineData("latitude=-90.5&longitude=0", "latitude")]
    [InlineData("latitude=0&longitude=181", "longitude")]
    [InlineData("latitude=0&longitude=-180.000001", "longitude")]
    [InlineData("latitude=41.1579", "longitude")]
    [InlineData("longitude=-8.6291", "latitude")]
    [InlineData("latitude=abc&longitude=-8.6291", "latitude")]
    [Trait("Requisito", "RF-F015-04")]
    public async Task SearchProviders_CoordenadasInvalidas_Devolve400NoCampo(string coordinates, string field)
    {
        int categoryId = await NewCategoryAsync();

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{coordinates}");

        await AssertFieldErrorAsync(response, field);
    }

    // ---------- Category (RF-F005-01, rule 2) ----------

    // CA6
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_SemCategoria_Devolve400NaCategoria()
    {
        using var response = await SearchAsync(KnownCoordinates);

        await AssertFieldErrorAsync(response, "serviceCategoryId");
    }

    // CA6
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_CategoriaInexistente_Devolve400NaCategoria()
    {
        using var response = await SearchAsync($"serviceCategoryId={int.MaxValue}&{KnownCoordinates}");

        await AssertFieldErrorAsync(response, "serviceCategoryId");
    }

    // CA6 (RF-F003-04, rule 1)
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    [Trait("Requisito", "RF-F003-04")]
    public async Task SearchProviders_CategoriaDesativada_Devolve400NaCategoria()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        using var deactivate = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"/api/categories/{categoryId}/deactivate", adminToken);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}");

        await AssertFieldErrorAsync(response, "serviceCategoryId");
    }

    // ---------- Name filter (RF-F009-02, rule 2) ----------

    // CA7 (RF-F009-02, criterion 2): part of the name, without telling upper and lower case apart.
    [Theory]
    [InlineData("SILVA")]
    [InlineData("silva")]
    [InlineData("  CANALIZAÇÕES  ")]
    [Trait("Requisito", "RF-F009-02")]
    public async Task SearchProviders_FiltroPeloNome_NaoDistingueMaiusculasDeMinusculas(string name)
    {
        int categoryId = await NewCategoryAsync();
        var silva = await CreateProviderAsync("Canalizações Silva", FakeGeocodingService.AddressAt10Km, 15, categoryId);
        await CreateProviderAsync("Jardins do Norte", FakeGeocodingService.AddressAt2Km, 15, categoryId);

        using var response = await SearchAsync(
            $"serviceCategoryId={categoryId}&{KnownCoordinates}&name={Uri.EscapeDataString(name)}");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.Equal(silva.ProfessionalProfileId, item.GetProperty("professionalProfileId").GetInt32());
    }

    // RF-F009-02, criterion 2: no provider has this text in the name. "%" and "_" are plain characters, not the
    // wildcards of LIKE.
    [Theory]
    [InlineData("Jardins")]
    [InlineData("%")]
    [InlineData("_")]
    [Trait("Requisito", "RF-F009-02")]
    public async Task SearchProviders_NomeSemCorrespondencia_DevolveListaVazia(string name)
    {
        int categoryId = await NewCategoryAsync();
        await CreateProviderAsync("Canalizações Silva", FakeGeocodingService.AddressAt2Km, 15, categoryId);

        using var response = await SearchAsync(
            $"serviceCategoryId={categoryId}&{KnownCoordinates}&name={Uri.EscapeDataString(name)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("total").GetInt32());
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    // ---------- Distance calculated in the database (DEC-06) ----------

    // CA8: on a meridian, 1° of latitude is R · π / 180 = 111.19 km.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_UmGrauDeLatitude_DevolveADistanciaDeReferencia()
    {
        int categoryId = await NewCategoryAsync();
        await CreateProviderAsync("No Porto", FakeGeocodingService.KnownAddress, 15, categoryId);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&latitude=42.1579&longitude=-8.6291");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.InRange(item.GetProperty("distanceKm").GetDecimal(), 111.19m - 0.1m, 111.19m + 0.1m);
    }

    // Points on opposite sides of the Earth (87.5° N, 180° and 87.5° S, 0°): the rounding errors can make the value
    // under the square root slightly greater than 1, and asin would fail without LEAST (GeoDistanceTranslation).
    // The result is half of the circumference, π · R = 20015.1 km.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_PontosOpostosDaTerra_DevolveMetadeDaCircunferencia()
    {
        int categoryId = await NewCategoryAsync();
        var provider = await CreateProviderAsync("Perto do polo", FakeGeocodingService.KnownAddress, 15, categoryId);
        await MoveAsync(provider.UserId, 87.5m, 180m);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&latitude=-87.5&longitude=0");

        var item = Assert.Single(await ItemsAsync(response));
        Assert.InRange(item.GetProperty("distanceKm").GetDecimal(), 20015.0m, 20015.2m);
    }

    // ---------- Pages (API documentation, section 4.4) ----------

    // The page is chosen after ordering by distance.
    [Fact]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_SegundaPagina_DevolveOTerceiroMaisProximoEOTotal()
    {
        int categoryId = await NewCategoryAsync();
        var at30 = await CreateProviderAsync("A 30 km", FakeGeocodingService.AddressAt30Km, 25, categoryId);
        await CreateProviderAsync("A 2 km", FakeGeocodingService.AddressAt2Km, 15, categoryId);
        await CreateProviderAsync("A 10 km", FakeGeocodingService.AddressAt10Km, 15, categoryId);

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.GetProperty("total").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(at30.ProfessionalProfileId, item.GetProperty("professionalProfileId").GetInt32());
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    [Trait("Requisito", "RF-F005-01")]
    public async Task SearchProviders_PaginaInvalida_Devolve400NoCampo(string query, string field)
    {
        int categoryId = await NewCategoryAsync();

        using var response = await SearchAsync($"serviceCategoryId={categoryId}&{KnownCoordinates}&{query}");

        await AssertFieldErrorAsync(response, field);
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

    // Coordinates that FakeGeocodingService does not know, written directly in the database.
    private async Task MoveAsync(int userId, decimal latitude, decimal longitude)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"User\" SET latitude = {latitude}, longitude = {longitude} WHERE \"userId\" = {userId}");
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