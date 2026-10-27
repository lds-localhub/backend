using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Services;

// F009 and F010: E20 (create a service with direct requests or a published job), and the use of the categories by
// the services in E11. The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ServicesEndpointsTests(LocalHubApiFactory factory)
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string UnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- Criteria 1 and 2: the two modalities ----------

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F009-03")]
    public async Task Create_SolicitacoesDiretasComDoisPrestadoresElegiveis_Devolve201ComServicoAbertoEDuasSolicitacoesPendentes()
    {
        var first = await CreateProviderWithProfileAsync(_client);
        var second = await CreateProviderInCategoryAsync(first.ServiceCategoryId);
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(first.ServiceCategoryId, first.ProfessionalProfileId, second);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        int serviceId = service.GetProperty("serviceId").GetInt32();
        Assert.Equal($"{ServicesUrl}/{serviceId}", response.Headers.Location?.OriginalString);
        Assert.Equal(client.UserId, service.GetProperty("clientId").GetInt32());
        Assert.Equal("DIRECT_REQUEST", service.GetProperty("proposalModel").GetString());
        Assert.Equal(first.ServiceCategoryId,
            service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.False(string.IsNullOrEmpty(service.GetProperty("serviceCategory").GetProperty("name").GetString()));
        Assert.Equal("Torneira da casa de banho com fuga de água.", service.GetProperty("description").GetString());
        Assert.Equal(Iso(Today.AddDays(1)), service.GetProperty("requestedStartDate").GetString());
        Assert.Equal(Iso(Today.AddDays(3)), service.GetProperty("requestedEndDate").GetString());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("actualCompletionDate").ValueKind);
        Assert.EndsWith("Z", service.GetProperty("creationDate").GetString(), StringComparison.Ordinal);

        var requests = service.GetProperty("directRequests").EnumerateArray().ToList();
        Assert.Equal(
            new[] { first.ProfessionalProfileId, second }.Order(),
            requests.Select(r => r.GetProperty("professionalProfileId").GetInt32()).Order());
        Assert.All(requests, r =>
        {
            Assert.True(r.GetProperty("directRequestId").GetInt32() > 0);
            Assert.Equal("PENDING", r.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("responseDate").ValueKind);
        });
        Assert.Equal((1, 2), await CountSavedAsync(client.UserId));

        // The creationDate returned is the one saved (PostgreSQL keeps microseconds), so E21 and E22 return it too.
        var returned = DateTimeOffset.Parse(service.GetProperty("creationDate").GetString()!, CultureInfo.InvariantCulture);
        Assert.Equal(await ReadCreationDateAsync(serviceId), returned);
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_TrabalhoPublicado_Devolve201AbertoComDirectRequestsVazio()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(categoryId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PUBLIC", service.GetProperty("proposalModel").GetString());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("requestedEndDate").ValueKind);
        Assert.Empty(service.GetProperty("directRequests").EnumerateArray());
        Assert.Equal((1, 0), await CountSavedAsync(client.UserId));
    }

    // ---------- Criterion 3: fields and dates ----------

    [Theory]
    [InlineData("serviceCategoryId", null)]
    [InlineData("title", null)]
    [InlineData("title", "   ")]
    [InlineData("title", "101")]
    [InlineData("description", null)]
    [InlineData("description", "   ")]
    [InlineData("description", "1001")]
    [InlineData("requestedStartDate", null)]
    [InlineData("requestedStartDate", "-1")]
    [InlineData("requestedEndDate", "0")]
    [InlineData("proposalModel", null)]
    [InlineData("location", "256")]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_CampoInvalido_Devolve400NoCampoENaoCriaServico(string field, string? value)
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);
        var body = PublicBody(categoryId);
        body[field] = field switch
        {
            // "101", "1001" and "256": a text with that number of characters.
            "title" or "description" or "location" when value is not null && int.TryParse(value, out int length) =>
                new string('a', length),
            // Days from today: the start yesterday, or the end before a start in two days.
            "requestedStartDate" when value is not null => Iso(Today.AddDays(int.Parse(value, CultureInfo.InvariantCulture))),
            "requestedEndDate" => Iso(Today.AddDays(int.Parse(value!, CultureInfo.InvariantCulture))),
            _ => value,
        };
        if (field == "requestedEndDate")
        {
            body["requestedStartDate"] = Iso(Today.AddDays(2));
        }

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        await AssertFieldErrorAsync(response, field);
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_VariosCamposInvalidos_Devolve400ComErroEmCadaCampo()
    {
        var client = await RegisterClientAsync(_client);
        var body = new Dictionary<string, object?> { ["proposalModel"] = "PUBLIC", ["description"] = "" };

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await AssertProblemAsync(response, BadRequestType, ServicesUrl)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("serviceCategoryId", out _));
        Assert.True(errors.TryGetProperty("description", out _));
        Assert.True(errors.TryGetProperty("requestedStartDate", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_DataUnicaIgualAHoje_Devolve201()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);
        var body = PublicBody(categoryId);
        body["requestedStartDate"] = Iso(Today);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Iso(Today), service.GetProperty("requestedStartDate").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    [Trait("Requisito", "RF-F003-03")]
    public async Task Create_CategoriaDesativada_Devolve400NaCategoria()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        using (var deactivate = await SendWithTokenAsync(
                   _client, HttpMethod.Post, $"/api/categories/{categoryId}/deactivate", adminToken))
        {
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        }

        var client = await RegisterClientAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(categoryId));

        await AssertFieldErrorAsync(response, "serviceCategoryId");
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_CategoriaInexistente_Devolve400NaCategoria()
    {
        var client = await RegisterClientAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(int.MaxValue));

        await AssertFieldErrorAsync(response, "serviceCategoryId");
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public async Task Create_CategoriaInexistenteEPrestadorDesativado_Devolve400ComOsDoisErros()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        int deactivated = await CreateProviderInCategoryAsync(provider.ServiceCategoryId, deactivate: true);
        var client = await RegisterClientAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, DirectRequestBody(int.MaxValue, deactivated));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await AssertProblemAsync(response, BadRequestType, ServicesUrl)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("serviceCategoryId", out _));
        var message = Assert.Single(errors.GetProperty("professionalProfileIds").EnumerateArray()).GetString();
        Assert.Contains("conta desativada", message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_CampoNaoPrevisto_Devolve400()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);
        var body = PublicBody(categoryId);
        body["status"] = "ASSIGNED";

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, BadRequestType, ServicesUrl);
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    // ---------- Criterion 4: location ----------

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_SemMoradaIndicada_UsaALocalizacaoRegistadaDoCliente()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(categoryId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        var me = await GetMeAsync(client.Token);
        Assert.Equal(me.GetProperty("address").GetString(), service.GetProperty("location").GetString());
        Assert.Equal(FakeGeocodingService.KnownLatitude, service.GetProperty("latitude").GetDecimal());
        Assert.Equal(FakeGeocodingService.KnownLongitude, service.GetProperty("longitude").GetDecimal());
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_ComMoradaIndicada_ConverteEmCoordenadasENaoAlteraALocalizacaoRegistada()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client, withLocation: false);
        var body = PublicBody(categoryId);
        body["location"] = FakeGeocodingService.KnownAddress;

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeGeocodingService.KnownAddress, service.GetProperty("location").GetString());
        Assert.Equal(FakeGeocodingService.KnownLatitude, service.GetProperty("latitude").GetDecimal());
        Assert.Equal(FakeGeocodingService.KnownLongitude, service.GetProperty("longitude").GetDecimal());

        var me = await GetMeAsync(client.Token);
        Assert.Equal(JsonValueKind.Null, me.GetProperty("address").ValueKind);
        Assert.Equal(JsonValueKind.Null, me.GetProperty("latitude").ValueKind);
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_SemMoradaNemLocalizacaoRegistada_Devolve400NaLocalizacao()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client, withLocation: false);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(categoryId));

        await AssertFieldErrorAsync(response, "location");
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task Create_MoradaNaoEncontrada_Devolve400NaLocalizacaoENaoCriaServico()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);
        var body = PublicBody(categoryId);
        body["location"] = "Rua que não existe 999";

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        await AssertFieldErrorAsync(response, "location");
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    // ---------- Criterion 5: chosen providers ----------

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public async Task Create_PrestadoresInelegiveis_Devolve400IdentificandoCadaPerfilENaoCriaNada()
    {
        var eligible = await CreateProviderWithProfileAsync(_client);
        int deactivated = await CreateProviderInCategoryAsync(eligible.ServiceCategoryId, deactivate: true);
        var otherCategory = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(eligible.ServiceCategoryId,
            eligible.ProfessionalProfileId, deactivated, otherCategory.ProfessionalProfileId, int.MaxValue);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        var messages = await AssertFieldErrorAsync(response, "professionalProfileIds");
        Assert.Equal(3, messages.Count);
        Assert.Contains(messages, m => m.Contains($"perfil {deactivated})", StringComparison.Ordinal)
                                       && m.Contains("conta desativada", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains($"perfil {otherCategory.ProfessionalProfileId})", StringComparison.Ordinal)
                                       && m.Contains("categoria", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains($"perfil {int.MaxValue} não existe", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.Contains($"perfil {eligible.ProfessionalProfileId}", StringComparison.Ordinal));
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    public async Task Create_MesmoPerfilRepetido_CriaUmaUnicaSolicitacao()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(provider.ServiceCategoryId,
            provider.ProfessionalProfileId, provider.ProfessionalProfileId);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        var request = Assert.Single(service.GetProperty("directRequests").EnumerateArray());
        Assert.Equal(provider.ProfessionalProfileId, request.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal((1, 1), await CountSavedAsync(client.UserId));
    }

    // ---------- Criterion 6: professionalProfileIds and the modality ----------

    [Theory]
    [InlineData("DIRECT_REQUEST", false)]
    [InlineData("DIRECT_REQUEST", true)]
    [InlineData("PUBLIC", true)]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_ListaDePerfisIncompativelComAModalidade_Devolve400NosPerfis(string model, bool emptyList)
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);
        var body = PublicBody(categoryId);
        body["proposalModel"] = model;
        if (emptyList)
        {
            body["professionalProfileIds"] = Array.Empty<int>();
        }

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        await AssertFieldErrorAsync(response, "professionalProfileIds");
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_TrabalhoPublicadoComPerfis_Devolve400NosPerfis()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        var body = PublicBody(provider.ServiceCategoryId);
        body["professionalProfileIds"] = new[] { provider.ProfessionalProfileId };

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        await AssertFieldErrorAsync(response, "professionalProfileIds");
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
    }

    // ---------- Criterion 7: nothing is created when the Nominatim is down ----------

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task Create_GeocodificacaoIndisponivel_Devolve503ENaoCriaServicoNemSolicitacoes()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        body["location"] = $"Rua das Flores 1, Porto {FakeGeocodingService.UnavailableMarker}";

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemAsync(response, UnavailableType, ServicesUrl);
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
        Assert.Equal(0, await CountDirectRequestsAsync(provider.ProfessionalProfileId));
    }

    // ---------- Criterion 8: roles (the 401 cases are in AccessControlTests) ----------

    [Fact]
    [Trait("Requisito", "RF-F009-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task Create_Prestador_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, provider.Token, PublicBody(provider.ServiceCategoryId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, ServicesUrl);
    }

    [Fact]
    [Trait("Requisito", "RF-F009-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task Create_Administrador_Devolve403()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, adminToken, PublicBody(categoryId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, ServicesUrl);
    }

    // ---------- E11: categories used by services in progress ----------

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public async Task DeactivateCategory_UsadaPorServicoEmCurso_Devolve409ComOMotivo()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        var client = await RegisterClientAsync(_client);
        using (var created = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(categoryId)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        string url = $"/api/categories/{categoryId}/deactivate";
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await AssertProblemAsync(response, ConflictType, url);
        Assert.Equal("A categoria está associada a 1 serviço em curso.", problem.GetProperty("detail").GetString());
    }

    private static string Iso(DateOnly date) => date.ToString(IsoDate, CultureInfo.InvariantCulture);

    private async Task<int> NewCategoryAsync() => await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));

    // A second provider with a profile in the given category; optionally with the account deactivated by the
    // administrator (E06). Returns the professionalProfileId.
    private async Task<int> CreateProviderInCategoryAsync(int serviceCategoryId, bool deactivate = false)
    {
        var (token, userId, _) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (deactivate)
        {
            string adminToken = await LoginAsAdminAsync(_client);
            using var deactivated = await SendWithTokenAsync(
                _client, HttpMethod.Post, $"/api/accounts/{userId}/deactivate", adminToken);
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        }

        return profile.GetProperty("professionalProfileId").GetInt32();
    }

    private async Task<JsonElement> GetMeAsync(string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/users/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // Returns the messages of the field, after checking that the response is a 400 with that field in errors.
    private static async Task<List<string>> AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ServicesUrl);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var messages),
            $"Esperado um erro em errors.{field}.");
        return messages.EnumerateArray().Select(m => m.GetString()!).ToList();
    }

    // Services of the client and their direct requests saved in the database (E21 and E23 are not implemented yet).
    private async Task<(int Services, int DirectRequests)> CountSavedAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        int services = await db.Services.CountAsync(s => s.ClientId == clientId);
        int directRequests = await db.DirectRequests.CountAsync(
            r => db.Services.Any(s => s.ServiceId == r.ServiceId && s.ClientId == clientId));
        return (services, directRequests);
    }

    private async Task<DateTimeOffset> ReadCreationDateAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Services.Where(s => s.ServiceId == serviceId).Select(s => s.CreationDate).SingleAsync();
    }

    private async Task<int> CountDirectRequestsAsync(int professionalProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.DirectRequests.CountAsync(r => r.ProfessionalProfileId == professionalProfileId);
    }
}