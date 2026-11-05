using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.UserLocations;

namespace LocalHub.IntegrationTests.Profiles;

// F004: E12 (create), E13 (get) and E14 (update), with the address and the radius of F015. Also the rule "provider
// without a profile" (API documentation, section 7) and the use of the categories by the profiles in E11.
// The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ProfilesEndpointsTests(LocalHubApiFactory factory)
{
    private const string MeUrl = "/api/profiles/me";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E12 ----------

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    [Trait("Requisito", "RF-F015-01")]
    public async Task Create_DadosValidos_Devolve201ComPerfilEGuardaLocalizacao()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string categoryName = $"Teste Criar {Guid.NewGuid():N}";
        int categoryId = await CreateCategoryAsync(_client, adminToken, categoryName);
        var (token, userId, email) = await RegisterProviderAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(categoryId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        int profileId = profile.GetProperty("professionalProfileId").GetInt32();
        Assert.Equal($"/api/profiles/{profileId}", response.Headers.Location?.OriginalString);
        Assert.Equal(userId, profile.GetProperty("serviceProviderId").GetInt32());
        Assert.Equal("Prestador Teste", profile.GetProperty("name").GetString());
        Assert.Equal("Canalizador com equipa própria na área do Grande Porto.", profile.GetProperty("introduction").GetString());
        Assert.Equal("Telefone 220 000 000", profile.GetProperty("contactDetails").GetString());
        Assert.Equal("15 anos em reparação de fugas.", profile.GetProperty("experience").GetString());
        var category = Assert.Single(profile.GetProperty("serviceCategories").EnumerateArray());
        Assert.Equal(categoryId, category.GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(categoryName, category.GetProperty("name").GetString());
        Assert.Equal(FakeGeocodingService.KnownAddress, profile.GetProperty("address").GetString());
        Assert.Equal(15, profile.GetProperty("serviceRadiusKm").GetInt32());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("averageRating").ValueKind);

        // The address is saved in User with its coordinates, and E04 and E02 return the professionalProfileId.
        var me = await GetJsonAsync("/api/auth/me", token);
        Assert.Equal(profileId, me.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal((FakeGeocodingService.KnownLatitude, FakeGeocodingService.KnownLongitude),
            await ReadUserCoordinatesAsync(factory, userId));

        using var login = await LoginAsync(_client, email, ValidPassword);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(profileId, loginBody.GetProperty("user").GetProperty("professionalProfileId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public async Task Create_SemApresentacaoContactoNemCategorias_Devolve400ComErroEmCadaCampoENaoCriaPerfil()
    {
        var (token, _, _) = await RegisterProviderAsync(_client);
        var body = ValidProfileBody();
        body["introduction"] = "  ";
        body.Remove("contactDetails");

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ProfilesUrl);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("introduction", out _));
        Assert.True(errors.TryGetProperty("contactDetails", out _));
        Assert.True(errors.TryGetProperty("serviceCategoryIds", out _));
        await AssertHasNoProfileAsync(token);
    }

    // ---------- E13 ----------

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task Get_ClientePerfilDePrestadorAtivo_Devolve200ComTodosOsCampos()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string clientToken = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), clientToken);

        Assert.Equal(provider.ProfessionalProfileId, profile.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal(provider.UserId, profile.GetProperty("serviceProviderId").GetInt32());
        foreach (string field in new[] { "name", "introduction", "contactDetails", "experience", "address" })
        {
            Assert.False(string.IsNullOrEmpty(profile.GetProperty(field).GetString()));
        }

        Assert.Equal([provider.ServiceCategoryId], CategoryIds(profile));
        Assert.Equal(15, profile.GetProperty("serviceRadiusKm").GetInt32());
        Assert.True(profile.TryGetProperty("averageRating", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    [Trait("Requisito", "RNF-06")]
    public async Task Get_PrestadorPerfilDeOutroPrestador_Devolve403()
    {
        var owner = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderWithProfileAsync(_client);
        string url = ProfileUrl(owner.ProfessionalProfileId);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, other.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    // ---------- E14 ----------

    [Fact]
    [Trait("Requisito", "RF-F004-02")]
    [Trait("Requisito", "RF-F004-04")]
    public async Task UpdateMine_DadosValidos_Devolve200EAlteracaoVisivelNaConsulta()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);
        int newCategory = await CreateCategoryAsync(_client, adminToken);
        var body = UpdateBody(newCategory);

        using var response = await SendJsonAsync(_client, HttpMethod.Put, MeUrl, provider.Token, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string clientToken = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");
        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), clientToken);
        Assert.Equal("Nova apresentação", profile.GetProperty("introduction").GetString());
        Assert.Equal("Novo contacto", profile.GetProperty("contactDetails").GetString());
        Assert.Equal("Nova experiência", profile.GetProperty("experience").GetString());
        Assert.Equal([newCategory], CategoryIds(profile));
        Assert.Equal(FakeGeocodingService.KnownAddress, profile.GetProperty("address").GetString());
        Assert.Equal(15, profile.GetProperty("serviceRadiusKm").GetInt32());
    }

    // ---------- Provider without a profile (API documentation, section 7) ----------

    [Theory]
    [InlineData("GET", "/api/profiles/1")]
    [InlineData("PUT", MeUrl)]
    [Trait("Requisito", "RF-F004-01")]
    public async Task EndpointDePrestador_PrestadorSemPerfil_Devolve403PerfilEmFalta(string method, string url)
    {
        var (token, _, _) = await RegisterProviderAsync(_client);
        // A valid body of E14: the profile filter is an action filter, so it runs after the validation of the body
        // (rule R11 of the Class Model).
        object body = method == "PUT" ? UpdateBody(1) : new { };

        using var response = await SendJsonAsync(_client, new HttpMethod(method), url, token, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, url);
    }

    // ---------- E11: categories used by profiles ----------

    private static string ProfileUrl(int professionalProfileId) => $"{ProfilesUrl}/{professionalProfileId}";

    private static Dictionary<string, object?> UpdateBody(params int[] serviceCategoryIds) => new()
    {
        ["introduction"] = "Nova apresentação",
        ["contactDetails"] = "Novo contacto",
        ["experience"] = "Nova experiência",
        ["serviceCategoryIds"] = serviceCategoryIds,
        ["address"] = FakeGeocodingService.KnownAddress,
        ["serviceRadiusKm"] = 15,
    };

    private static List<int> CategoryIds(JsonElement profile) =>
        profile.GetProperty("serviceCategories").EnumerateArray()
            .Select(c => c.GetProperty("serviceCategoryId").GetInt32())
            .ToList();

    private async Task<JsonElement> GetJsonAsync(string url, string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task AssertHasNoProfileAsync(string token)
    {
        var me = await GetJsonAsync("/api/auth/me", token);
        Assert.Equal(JsonValueKind.Null, me.GetProperty("professionalProfileId").ValueKind);
    }

    // action: "deactivate" (E06) or "activate" (E07).
    private async Task SetAccountStatusAsync(string adminToken, int userId, string action)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"/api/accounts/{userId}/{action}", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}