using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;

namespace LocalHub.IntegrationTests.Profiles;

// F004: E12 (create), E13 (get) and E14 (update) and the radius of F015: E17. Also the rule "provider without a
// profile" (API documentation, section 7) and the use of the categories by the profiles in E11.
// The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ProfilesEndpointsTests(LocalHubApiFactory factory)
{
    private const string MeUrl = "/api/profiles/me";
    private const string RadiusUrl = "/api/profiles/me/service-radius";
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
        var me = await GetJsonAsync("/api/users/me", token);
        Assert.Equal(profileId, me.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal(FakeGeocodingService.KnownLatitude, me.GetProperty("latitude").GetDecimal());
        Assert.Equal(FakeGeocodingService.KnownLongitude, me.GetProperty("longitude").GetDecimal());

        using var login = await LoginAsync(_client, email, ValidPassword);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(profileId, loginBody.GetProperty("user").GetProperty("professionalProfileId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public async Task Create_PrestadorQueJaTemPerfil_Devolve409()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, provider.Token, ValidProfileBody(provider.ServiceCategoryId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ConflictType, ProfilesUrl);
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

    [Theory]
    [InlineData("address", null)]
    [InlineData("serviceRadiusKm", 0)]
    [InlineData("serviceRadiusKm", 101)]
    [InlineData("serviceRadiusKm", 25.5)]
    [InlineData("serviceRadiusKm", null)]
    [Trait("Requisito", "RF-F004-01")]
    [Trait("Requisito", "RF-F015-01")]
    public async Task Create_MoradaOuRaioInvalido_Devolve400NoCampo(string field, object? value)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        var (token, _, _) = await RegisterProviderAsync(_client);
        var body = ValidProfileBody(categoryId);
        body[field] = value;

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ProfilesUrl);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public async Task Create_CategoriaDesativada_Devolve400NasCategorias()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int active = await CreateCategoryAsync(_client, adminToken);
        int inactive = await CreateCategoryAsync(_client, adminToken);
        await DeactivateCategoryAsync(adminToken, inactive);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(active, inactive));

        await AssertCategoryErrorAsync(response, ProfilesUrl);
        await AssertHasNoProfileAsync(token);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public async Task Create_CategoriaInexistente_Devolve400NasCategorias()
    {
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(int.MaxValue));

        await AssertCategoryErrorAsync(response, ProfilesUrl);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public async Task Create_TresCategoriasAtivasComRepeticao_AssociaAsTresUmaVez()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int first = await CreateCategoryAsync(_client, adminToken);
        int second = await CreateCategoryAsync(_client, adminToken);
        int third = await CreateCategoryAsync(_client, adminToken);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(first, second, third, first));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([first, second, third], CategoryIds(profile).Order());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task Create_MoradaNaoEncontrada_Devolve400NaMoradaENaoCriaPerfil()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        var (token, _, _) = await RegisterProviderAsync(_client);
        var body = ValidProfileBody(categoryId);
        body["address"] = "Rua Que Não Existe 999";

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ProfilesUrl);
        Assert.True(problem.GetProperty("errors").TryGetProperty("address", out _));
        await AssertHasNoProfileAsync(token);
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public async Task Create_GeocodificacaoIndisponivel_Devolve503ENaoCriaPerfil()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        var (token, _, _) = await RegisterProviderAsync(_client);
        var body = ValidProfileBody(categoryId);
        body["address"] = $"Rua do Almada {FakeGeocodingService.UnavailableMarker}";

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertHasNoProfileAsync(token);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task Create_Cliente_Devolve403()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, ProfilesUrl);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task Create_Administrador_Devolve403()
    {
        string token = await LoginAsAdminAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
    public async Task Get_PrestadorProprioPerfil_Devolve200()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), provider.Token);

        Assert.Equal(provider.ProfessionalProfileId, profile.GetProperty("professionalProfileId").GetInt32());
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

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task Get_PerfilInexistente_Devolve404()
    {
        string clientToken = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");
        string url = ProfileUrl(int.MaxValue);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, clientToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task Get_ClientePrestadorComContaDesativada_Devolve404()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);
        await SetAccountStatusAsync(adminToken, provider.UserId, "deactivate");
        string clientToken = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, ProfileUrl(provider.ProfessionalProfileId), clientToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task Get_Administrador_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, ProfileUrl(provider.ProfessionalProfileId), adminToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        // The address and the radius are not changed by E14.
        Assert.Equal(FakeGeocodingService.KnownAddress, profile.GetProperty("address").GetString());
        Assert.Equal(15, profile.GetProperty("serviceRadiusKm").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public async Task UpdateMine_SemCategorias_Devolve400EMantemAsCategorias()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Put, MeUrl, provider.Token, UpdateBody());

        await AssertCategoryErrorAsync(response, MeUrl);
        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), provider.Token);
        Assert.Equal([provider.ServiceCategoryId], CategoryIds(profile));
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public async Task UpdateMine_CategoriaNovaDesativada_Devolve400()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);
        int inactive = await CreateCategoryAsync(_client, adminToken);
        await DeactivateCategoryAsync(adminToken, inactive);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Put, MeUrl, provider.Token, UpdateBody(provider.ServiceCategoryId, inactive));

        await AssertCategoryErrorAsync(response, MeUrl);
    }

    // A category can only be deactivated when no active provider uses it, so the provider is deactivated first.
    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    [Trait("Requisito", "RF-F003-03")]
    public async Task UpdateMine_CategoriaJaAssociadaEntretantoDesativada_PodeManterSe()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);
        await SetAccountStatusAsync(adminToken, provider.UserId, "deactivate");
        await DeactivateCategoryAsync(adminToken, provider.ServiceCategoryId);
        await SetAccountStatusAsync(adminToken, provider.UserId, "activate");
        string token = await LoginAndGetTokenAsync(_client, provider.Email, ValidPassword);
        int newCategory = await CreateCategoryAsync(_client, adminToken);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Put, MeUrl, token, UpdateBody(provider.ServiceCategoryId, newCategory));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { provider.ServiceCategoryId, newCategory }.Order(), CategoryIds(profile).Order());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-02")]
    public async Task UpdateMine_ClassificacaoMediaNoCorpo_Devolve400()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var body = UpdateBody(provider.ServiceCategoryId);
        body["averageRating"] = 5.0;

        using var response = await SendJsonAsync(_client, HttpMethod.Put, MeUrl, provider.Token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, BadRequestType, MeUrl);
        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), provider.Token);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("averageRating").ValueKind);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-02")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task UpdateMine_Cliente_Devolve403()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await SendJsonAsync(_client, HttpMethod.Put, MeUrl, token, UpdateBody(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, MeUrl);
    }

    // ---------- E17 ----------

    [Fact]
    [Trait("Requisito", "RF-F015-01")]
    public async Task SetServiceRadius_25Km_Devolve200EFicaRegistado()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Put, RadiusUrl, provider.Token, new { serviceRadiusKm = 25 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, body.GetProperty("serviceRadiusKm").GetInt32());
        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), provider.Token);
        Assert.Equal(25, profile.GetProperty("serviceRadiusKm").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(150)]
    [InlineData(25.5)]
    [Trait("Requisito", "RF-F015-01")]
    public async Task SetServiceRadius_ForaDoIntervaloOuNaoInteiro_Devolve400EMantemORaio(double km)
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Put, RadiusUrl, provider.Token, new { serviceRadiusKm = km });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, RadiusUrl);
        Assert.True(problem.GetProperty("errors").TryGetProperty("serviceRadiusKm", out _));
        var profile = await GetJsonAsync(ProfileUrl(provider.ProfessionalProfileId), provider.Token);
        Assert.Equal(15, profile.GetProperty("serviceRadiusKm").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-01")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task SetServiceRadius_Cliente_Devolve403()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await SendJsonAsync(_client, HttpMethod.Put, RadiusUrl, token, new { serviceRadiusKm = 25 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Provider without a profile (API documentation, section 7) ----------

    [Theory]
    [InlineData("GET", "/api/profiles/1")]
    [InlineData("PUT", MeUrl)]
    [InlineData("PUT", RadiusUrl)]
    [InlineData("PUT", "/api/users/me/location")]
    [Trait("Requisito", "RF-F004-01")]
    public async Task EndpointDePrestador_PrestadorSemPerfil_Devolve403PerfilEmFalta(string method, string url)
    {
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await SendJsonAsync(_client, new HttpMethod(method), url, token, new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public async Task GetMe_PrestadorSemPerfil_DevolveProfessionalProfileIdNulo()
    {
        var (token, _, _) = await RegisterProviderAsync(_client);

        await AssertHasNoProfileAsync(token);
    }

    // ---------- E11: categories used by profiles ----------

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public async Task DeactivateCategory_UsadaPorPrestadorAtivo_Devolve409ComOMotivo()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);
        string url = $"/api/categories/{provider.ServiceCategoryId}/deactivate";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await AssertProblemAsync(response, ConflictType, url);
        Assert.Equal("A categoria está associada a 1 Prestador ativo.", problem.GetProperty("detail").GetString());
    }

    private static string ProfileUrl(int professionalProfileId) => $"{ProfilesUrl}/{professionalProfileId}";

    private static Dictionary<string, object?> UpdateBody(params int[] serviceCategoryIds) => new()
    {
        ["introduction"] = "Nova apresentação",
        ["contactDetails"] = "Novo contacto",
        ["experience"] = "Nova experiência",
        ["serviceCategoryIds"] = serviceCategoryIds,
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
        var me = await GetJsonAsync("/api/users/me", token);
        Assert.Equal(JsonValueKind.Null, me.GetProperty("professionalProfileId").ValueKind);
    }

    private static async Task AssertCategoryErrorAsync(HttpResponseMessage response, string url)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, url);
        Assert.True(problem.GetProperty("errors").TryGetProperty("serviceCategoryIds", out _));
    }

    private async Task DeactivateCategoryAsync(string adminToken, int serviceCategoryId)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"/api/categories/{serviceCategoryId}/deactivate", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // action: "deactivate" (E06) or "activate" (E07).
    private async Task SetAccountStatusAsync(string adminToken, int userId, string action)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"/api/accounts/{userId}/{action}", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}