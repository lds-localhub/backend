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

namespace LocalHub.IntegrationTests.Search;

// F006: E19 GET /api/search/services and E38 GET /api/search/services/{serviceId}, with the geocoding replaced by
// FakeGeocodingService. The provider is at KnownAddress and the jobs at 2, 10 and 30 km to the north of it. Each test
// uses its own category, so it only finds the jobs that it creates. The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class PublishedJobsEndpointsTests(LocalHubApiFactory factory)
{
    private const string Url = "/api/search/services";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E19: who can search (criterion 1) ----------

    [Theory]
    [InlineData("CLIENT")]
    [InlineData("ADMIN")]
    [Trait("Requisito", "RF-F006-01")]
    public async Task SearchJobs_PapelDiferenteDePrestador_Devolve403(string role)
    {
        string token = role == "ADMIN"
            ? await LoginAsAdminAsync(_client)
            : await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await GetAsync(token, Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, Url);
    }

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RF-F004-01")]
    public async Task SearchJobs_PrestadorSemPerfil_Devolve403PerfilEmFalta()
    {
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await GetAsync(token, Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, Url);
    }

    // ---------- E19: which jobs are returned (criteria 2 and 6) ----------

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RF-F002-02")]
    public async Task SearchJobs_SoDevolveTrabalhosPublicadosAbertosDeClientesAtivos()
    {
        var provider = await CreateProviderAsync(15);
        int eligible = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        int assigned = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await SetServiceStatusAsync(assigned, "ASSIGNED");
        int completed = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await SetServiceStatusAsync(completed, "COMPLETED");
        int cancelled = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await SetServiceStatusAsync(cancelled, "CANCELLED");
        var deactivatedClient = await RegisterClientAsync(_client);
        await CreateJobAsync(deactivatedClient, provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await DeactivateAsync(deactivatedClient.UserId);
        // A service with direct requests to the provider itself is not a published job either.
        await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);

        using var response = await GetAsync(provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}");

        var body = await BodyAsync(response);
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(eligible, item.GetProperty("serviceId").GetInt32());
    }

    // Criterion 3 and RNF-06: on a meridian the distances are the known values R · Δφ; no data of the Client.
    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RNF-06")]
    public async Task SearchJobs_TrabalhosA2_10E30Km_DevolveOrdenadosPorDistanciaSemDadosDoCliente()
    {
        var provider = await CreateProviderAsync(15);
        // Created out of order, so the order of the results can only come from the distance.
        int at30 = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt30Km);
        int at2 = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        int at10 = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt10Km);

        using var response = await GetAsync(provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}");

        var body = await BodyAsync(response);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.GetProperty("total").GetInt32());
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([at2, at10, at30], ServiceIds(items));
        Assert.Equal([2.0m, 10.0m, 30.0m], items.Select(i => i.GetProperty("distanceKm").GetDecimal()));
        Assert.Equal([true, true, false], items.Select(i => i.GetProperty("withinServiceRadius").GetBoolean()));

        var first = items[0];
        Assert.Equal(provider.ServiceCategoryId,
            first.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("Substituir torneira", first.GetProperty("title").GetString());
        Assert.Equal("Substituir a torneira da cozinha que pinga.", first.GetProperty("description").GetString());
        Assert.Equal(FakeGeocodingService.AddressAt2Km, first.GetProperty("location").GetString());
        Assert.Equal(Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            first.GetProperty("requestedStartDate").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("requestedEndDate").ValueKind);
        Assert.EndsWith("Z", first.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        AssertHasNoClientData(first);
    }

    // RF-F006-01: without serviceCategoryId the jobs of every category are returned. The provider and the jobs are
    // moved to coordinates that no other test uses, so the first results are the jobs of this test.
    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    public async Task SearchJobs_SemCategoria_DevolveTrabalhosDeTodasAsCategorias()
    {
        var provider = await CreateProviderAsync(15);
        int otherCategoryId = await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));
        int near = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        int far = await CreateJobAsync(otherCategoryId, FakeGeocodingService.AddressAt2Km);
        // 0.01° of latitude is about 1.1 km.
        decimal latitude = 37.0m + (Random.Shared.Next(0, 100_000) / 100_000m);
        await MoveUserAsync(provider.UserId, latitude, -20.0m);
        await MoveServiceAsync(far, latitude + 0.02m, -20.0m);
        await MoveServiceAsync(near, latitude + 0.01m, -20.0m);

        using var response = await GetAsync(provider.Token, $"{Url}?pageSize=2");

        var items = (await BodyAsync(response)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([near, far], ServiceIds(items));
        Assert.Equal(
            [provider.ServiceCategoryId, otherCategoryId],
            items.Select(i => i.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32()));
    }

    // ---------- E19: filters and pages (criteria 4 and 5) ----------

    // Criterion 5: with a radius of 25 km, the job at 10 km is returned and the one at 30 km is not; without the
    // filter (previous test) a job outside the radius is returned.
    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    public async Task SearchJobs_WithinServiceRadiusTrueERaioDe25_SoDevolveOsTrabalhosDentroDoRaio()
    {
        var provider = await CreateProviderAsync(25);
        int at10 = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt10Km);
        await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt30Km);

        using var withFilter = await GetAsync(
            provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}&withinServiceRadius=true");
        using var withoutFilter = await GetAsync(
            provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}&withinServiceRadius=false");

        var filtered = await BodyAsync(withFilter);
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
        var item = Assert.Single(filtered.GetProperty("items").EnumerateArray());
        Assert.Equal(at10, item.GetProperty("serviceId").GetInt32());
        Assert.True(item.GetProperty("withinServiceRadius").GetBoolean());
        Assert.Equal(2, (await BodyAsync(withoutFilter)).GetProperty("total").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    public async Task SearchJobs_SegundaPagina_DevolveOTrabalhoSeguinte()
    {
        var provider = await CreateProviderAsync(15);
        await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        int at10 = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt10Km);

        using var response = await GetAsync(
            provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}&page=2&pageSize=1");

        var body = await BodyAsync(response);
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("total").GetInt32());
        Assert.Equal([at10], ServiceIds(body.GetProperty("items").EnumerateArray().ToList()));
    }

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    public async Task SearchJobs_CategoriaDesativada_Devolve400NoCampo()
    {
        var provider = await CreateProviderAsync(15);
        string adminToken = await LoginAsAdminAsync(_client);
        int inactiveId = await CreateCategoryAsync(_client, adminToken);
        using (var deactivate = await SendWithTokenAsync(
                   _client, HttpMethod.Post, $"/api/categories/{inactiveId}/deactivate", adminToken))
        {
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        }

        using var response = await GetAsync(provider.Token, $"{Url}?serviceCategoryId={inactiveId}");

        await AssertFieldErrorAsync(response, "serviceCategoryId");
    }

    [Theory]
    [InlineData("serviceCategoryId=999999", "serviceCategoryId")]
    [InlineData("serviceCategoryId=abc", "serviceCategoryId")]
    [InlineData("withinServiceRadius=talvez", "withinServiceRadius")]
    [InlineData("page=0", "page")]
    [Trait("Requisito", "RF-F006-01")]
    public async Task SearchJobs_FiltroInvalido_Devolve400NoCampo(string query, string field)
    {
        var provider = await CreateProviderAsync(15);

        using var response = await GetAsync(provider.Token, $"{Url}?{query}");

        await AssertFieldErrorAsync(response, field);
    }

    // ---------- E38: consult a published job (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetJob_TrabalhoPublicadoAberto_Devolve200ComODetalheESemDadosDoCliente()
    {
        var provider = await CreateProviderAsync(15);
        int serviceId = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt10Km);

        using var response = await GetAsync(provider.Token, $"{Url}/{serviceId}");

        var job = await BodyAsync(response);
        Assert.Equal(serviceId, job.GetProperty("serviceId").GetInt32());
        Assert.Equal(provider.ServiceCategoryId, job.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("Substituir torneira", job.GetProperty("title").GetString());
        Assert.Equal("Substituir a torneira da cozinha que pinga.", job.GetProperty("description").GetString());
        Assert.Equal(FakeGeocodingService.AddressAt10Km, job.GetProperty("location").GetString());
        Assert.Equal(10.0m, job.GetProperty("distanceKm").GetDecimal());
        Assert.True(job.GetProperty("withinServiceRadius").GetBoolean());
        Assert.Equal(Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            job.GetProperty("requestedStartDate").GetString());
        Assert.Equal(JsonValueKind.Null, job.GetProperty("requestedEndDate").ValueKind);
        Assert.EndsWith("Z", job.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, job.GetProperty("myProposalId").ValueKind);
        AssertHasNoClientData(job);
        Assert.False(job.TryGetProperty("proposals", out _));
    }

    // myProposalId is the own proposal; another provider never sees it (RNF-06).
    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetJob_DepoisDaProposta_DevolveMyProposalIdSoAoPrestadorQueAApresentou()
    {
        var provider = await CreateProviderAsync(15);
        var other = await CreateProviderAsync(15, provider.ServiceCategoryId);
        int serviceId = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        using var submit = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
            provider.Token, new { amount = 120.00m, conditions = "Material incluído." });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        int proposalId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();

        using var mine = await GetAsync(provider.Token, $"{Url}/{serviceId}");
        using var others = await GetAsync(other.Token, $"{Url}/{serviceId}");

        Assert.Equal(proposalId, (await BodyAsync(mine)).GetProperty("myProposalId").GetInt32());
        Assert.Equal(JsonValueKind.Null, (await BodyAsync(others)).GetProperty("myProposalId").ValueKind);
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("solicitacoes-diretas")]
    [InlineData("ASSIGNED")]
    [InlineData("CANCELLED")]
    [InlineData("cliente-desativado")]
    [Trait("Requisito", "RF-F011-01")]
    [Trait("Requisito", "RF-F002-02")]
    public async Task GetJob_ServicoQueNaoEUmTrabalhoPublicadoAberto_Devolve404(string scenario)
    {
        var provider = await CreateProviderAsync(15);
        int serviceId;
        switch (scenario)
        {
            case "inexistente":
                serviceId = int.MaxValue;
                break;
            case "solicitacoes-diretas":
                serviceId = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
                break;
            case "cliente-desativado":
                var client = await RegisterClientAsync(_client);
                serviceId = await CreateJobAsync(client, provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
                await DeactivateAsync(client.UserId);
                break;
            default:
                serviceId = await CreateJobAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
                await SetServiceStatusAsync(serviceId, scenario);
                break;
        }

        using var response = await GetAsync(provider.Token, $"{Url}/{serviceId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, $"{Url}/{serviceId}");
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    public async Task GetJob_Cliente_Devolve403()
    {
        var client = await RegisterClientAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));
        int serviceId = await CreateJobAsync(client, categoryId, FakeGeocodingService.AddressAt2Km);

        using var response = await GetAsync(client.Token, $"{Url}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    [Trait("Requisito", "RF-F004-01")]
    public async Task GetJob_PrestadorSemPerfil_Devolve403PerfilEmFalta()
    {
        int categoryId = await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));
        int serviceId = await CreateJobAsync(categoryId, FakeGeocodingService.AddressAt2Km);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await GetAsync(token, $"{Url}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, $"{Url}/{serviceId}");
    }

    // ---------- Helpers ----------

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    // A provider at KnownAddress with the radius given, in a new category or in the one given.
    private async Task<ProviderWithProfile> CreateProviderAsync(int serviceRadiusKm, int? serviceCategoryId = null)
    {
        int categoryId = serviceCategoryId ?? await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));
        var (token, userId, email) = await RegisterProviderAsync(_client);
        var body = ValidProfileBody(categoryId);
        body["serviceRadiusKm"] = serviceRadiusKm;
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryId);
    }

    // A published job (E20) at the address given, by a new client.
    private async Task<int> CreateJobAsync(int serviceCategoryId, string address) =>
        await CreateJobAsync(await RegisterClientAsync(_client), serviceCategoryId, address);

    private async Task<int> CreateJobAsync(TestClient client, int serviceCategoryId, string address)
    {
        var body = PublicBody(serviceCategoryId);
        body["location"] = address;
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task<int> CreateDirectServiceAsync(int serviceCategoryId, int professionalProfileId)
    {
        var client = await RegisterClientAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, DirectRequestBody(serviceCategoryId, professionalProfileId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task DeactivateAsync(int userId)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, $"/api/accounts/{userId}/deactivate", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The endpoints that assign, complete or cancel a service are later issues, so the state is changed in the database.
    // A COMPLETED service must have the actual completion date (CK_Service_actualCompletionDate).
    private async Task SetServiceStatusAsync(int serviceId, string status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE "Service"
             SET "status" = {status}::service_status,
                 "actualCompletionDate" = CASE WHEN {status} = 'COMPLETED' THEN now() END
             WHERE "serviceId" = {serviceId}
             """);
    }

    // Coordinates that FakeGeocodingService does not know, written directly in the database.
    private async Task MoveUserAsync(int userId, decimal latitude, decimal longitude)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"User\" SET latitude = {latitude}, longitude = {longitude} WHERE \"userId\" = {userId}");
    }

    private async Task MoveServiceAsync(int serviceId, decimal latitude, decimal longitude)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Service\" SET latitude = {latitude}, longitude = {longitude} WHERE \"serviceId\" = {serviceId}");
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<int> ServiceIds(List<JsonElement> items) =>
        items.Select(i => i.GetProperty("serviceId").GetInt32()).ToList();

    // RF-F006-01 and RF-F011-01: neither the Client nor its contacts.
    private static void AssertHasNoClientData(JsonElement job)
    {
        foreach (string field in new[] { "clientId", "client", "clientName", "email", "contactDetails" })
        {
            Assert.False(job.TryGetProperty(field, out _), $"O trabalho não pode ter o campo {field}.");
        }
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Esperado um erro em errors.{field}.");
    }
}