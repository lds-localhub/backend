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

namespace LocalHub.IntegrationTests.Reports;

// F013: E33 (activity report of the client). Each test uses a new client, so the report only has its services.
// The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ReportsEndpointsTests(LocalHubApiFactory factory)
{
    private const string ReportUrl = "/api/reports/client";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- RF-F013-01: criterion 1 ----------

    // One ASSIGNED, one COMPLETED and one CANCELLED after being assigned; an OPEN service, with a pending proposal, and
    // a service cancelled while OPEN are not contracted.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task Relatorio_TresServicosContratados_ContaUmEmCadaEstadoEIgnoraOsNaoAtribuidos()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedAsync(client, provider, 80.00m);
        int completed = await CreateContractedAsync(client, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{completed}/complete");
        int cancelled = await CreateContractedAsync(client, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelled}/cancel");
        int open = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(open, provider, 80.00m);
        int cancelledOpen = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(cancelledOpen, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelledOpen}/cancel");

        var report = await GetReportAsync(client.Token);

        Assert.Equal(3, report.GetProperty("contractedServices").GetInt32());
        var byStatus = report.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("status").GetString()!, s => s.GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["ASSIGNED"] = 1, ["COMPLETED"] = 1, ["CANCELLED"] = 1 }, byStatus);
    }

    // ---------- RF-F013-01: criterion 5 ----------

    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task Relatorio_DoisDeUmaCategoriaEUmDeOutra_DistribuiPorCategoria()
    {
        var plumbing = await CreateProviderAsync($"Canalização {Guid.NewGuid():N}");
        var gardening = await CreateProviderAsync($"Jardinagem {Guid.NewGuid():N}");
        var client = await RegisterClientAsync(_client);
        await CreateContractedAsync(client, plumbing, 80.00m);
        await CreateContractedAsync(client, plumbing, 80.00m);
        await CreateContractedAsync(client, gardening, 80.00m);

        var report = await GetReportAsync(client.Token);

        var byCategory = report.GetProperty("byCategory").EnumerateArray().ToList();
        Assert.Equal(2, byCategory.Count);
        Assert.Equal(plumbing.ServiceCategoryId, byCategory[0].GetProperty("serviceCategoryId").GetInt32());
        Assert.StartsWith("Canalização", byCategory[0].GetProperty("name").GetString());
        Assert.Equal(2, byCategory[0].GetProperty("count").GetInt32());
        Assert.Equal(gardening.ServiceCategoryId, byCategory[1].GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(1, byCategory[1].GetProperty("count").GetInt32());
    }

    // ---------- RF-F013-01: criteria 2 and 3 ----------

    // 80,00 € + 150,00 € = 230,00 €; a pending proposal of 500,00 € does not count.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task Relatorio_PropostasAceitesDe80E150_ValorTotalE230()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedAsync(client, provider, 80.00m);
        int cancelled = await CreateContractedAsync(client, provider, 150.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelled}/cancel");
        int open = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(open, provider, 500.00m);

        var report = await GetReportAsync(client.Token);

        Assert.Equal(230.00m, report.GetProperty("totalAcceptedAmount").GetDecimal());
    }

    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task Relatorio_HistoricoTemSoOServicoRealizadoComDataCategoriaPrestadorEValor()
    {
        string categoryName = $"Canalização {Guid.NewGuid():N}";
        var provider = await CreateProviderAsync(categoryName);
        var client = await RegisterClientAsync(_client);
        int completed = await CreateContractedAsync(client, provider, 80.00m);
        var completion = await PostOkAsync(client.Token, $"{ServicesUrl}/{completed}/complete");
        await CreateContractedAsync(client, provider, 150.00m);

        var report = await GetReportAsync(client.Token);

        var item = Assert.Single(report.GetProperty("completedHistory").EnumerateArray());
        Assert.Equal(completed, item.GetProperty("serviceId").GetInt32());
        Assert.Equal(categoryName, item.GetProperty("serviceCategory").GetString());
        Assert.Equal("Prestador Teste", item.GetProperty("provider").GetString());
        Assert.Equal(80.00m, item.GetProperty("amount").GetDecimal());
        Assert.Equal(completion.GetProperty("actualCompletionDate").GetDateTimeOffset(),
            item.GetProperty("actualCompletionDate").GetDateTimeOffset());
    }

    // ---------- RF-F013-01: rule 5 ----------

    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task Relatorio_SoIncluiOsServicosDoProprioCliente()
    {
        var provider = await CreateProviderAsync();
        var other = await RegisterClientAsync(_client);
        await CreateContractedAsync(other, provider, 80.00m);
        var client = await RegisterClientAsync(_client);

        var report = await GetReportAsync(client.Token);

        Assert.Equal(0, report.GetProperty("contractedServices").GetInt32());
        Assert.Empty(report.GetProperty("byCategory").EnumerateArray());
        Assert.All(report.GetProperty("byStatus").EnumerateArray(), s => Assert.Equal(0, s.GetProperty("count").GetInt32()));
        Assert.Equal(0m, report.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Empty(report.GetProperty("completedHistory").EnumerateArray());
    }

    // Criterion 4 of RF-F013-01 is ensured by the route: there is no parameter to choose the client.
    [Theory]
    [Trait("Requisito", "RF-F013-01")]
    [InlineData("PROVIDER")]
    [InlineData("ADMINISTRATOR")]
    public async Task Relatorio_PapelDiferenteDeCliente_Devolve403(string role)
    {
        string token = role == "ADMINISTRATOR"
            ? await LoginAsAdminAsync(_client)
            : await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReportUrl, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- RF-F013-02 ----------

    // Criteria 1 and 3: the day of creation is the day in Portugal (UTC+1 on 1 October, UTC+0 on 31 October).
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public async Task Relatorio_PeriodoDeOutubro_SoContaOsServicosCriadosNesseMesEmPortugal()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int september = await CreateContractedAsync(client, provider, 10.00m);
        int firstOfOctober = await CreateContractedAsync(client, provider, 20.00m);
        int lastOfOctober = await CreateContractedAsync(client, provider, 40.00m);
        int november = await CreateContractedAsync(client, provider, 80.00m);
        await SetCreationDateAsync(september, new DateTimeOffset(2026, 9, 30, 22, 59, 0, TimeSpan.Zero));
        await SetCreationDateAsync(firstOfOctober, new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero));
        await SetCreationDateAsync(lastOfOctober, new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        await SetCreationDateAsync(november, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        var october = await GetReportAsync(client.Token, "?startDate=2026-10-01&endDate=2026-10-31");
        var all = await GetReportAsync(client.Token);

        Assert.Equal(2, october.GetProperty("contractedServices").GetInt32());
        Assert.Equal(60.00m, october.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Equal("2026-10-01", october.GetProperty("period").GetProperty("startDate").GetString());
        Assert.Equal("2026-10-31", october.GetProperty("period").GetProperty("endDate").GetString());
        Assert.Equal(4, all.GetProperty("contractedServices").GetInt32());
        Assert.Equal(JsonValueKind.Null, all.GetProperty("period").GetProperty("startDate").ValueKind);
    }

    // Only one of the dates: the period is open on the other side.
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public async Task Relatorio_SoComDataInicial_ContaOsServicosDesdeEssaData()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int old = await CreateContractedAsync(client, provider, 10.00m);
        await CreateContractedAsync(client, provider, 20.00m);
        await SetCreationDateAsync(old, new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        var report = await GetReportAsync(client.Token, "?startDate=2026-10-01");

        Assert.Equal(1, report.GetProperty("contractedServices").GetInt32());
        Assert.Equal(20.00m, report.GetProperty("totalAcceptedAmount").GetDecimal());
    }

    // Criterion 2 and the invalid dates: a global error of the operation, without "errors".
    [Theory]
    [Trait("Requisito", "RF-F013-02")]
    [InlineData("?startDate=2026-10-31&endDate=2026-10-01", "A data inicial não pode ser posterior à data final.")]
    [InlineData("?startDate=2026-13-01", "As datas do período têm de estar no formato AAAA-MM-DD.")]
    [InlineData("?endDate=01-10-2026", "As datas do período têm de estar no formato AAAA-MM-DD.")]
    public async Task Relatorio_PeriodoInvalido_Devolve400ComErroGlobal(string query, string detail)
    {
        var client = await RegisterClientAsync(_client, withLocation: false);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReportUrl + query, client.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ReportUrl);
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    // ---------- Helpers ----------

    private async Task<JsonElement> GetReportAsync(string token, string query = "")
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReportUrl + query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> PostOkAsync(string token, string url)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // A provider with a profile in a new category, optionally with the given name.
    private async Task<ProviderWithProfile> CreateProviderAsync(string? categoryName = null)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken, categoryName);
        var (token, userId, email) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(categoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryId);
    }

    private async Task<int> CreateJobAsync(TestClient client, int serviceCategoryId)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task<int> SubmitAsync(int serviceId, ProviderWithProfile provider, decimal amount)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
            provider.Token, new { amount, conditions = "Material incluído." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();
    }

    // A published job of the client with the proposal of the provider accepted (ASSIGNED).
    private async Task<int> CreateContractedAsync(TestClient client, ProviderWithProfile provider, decimal amount)
    {
        int serviceId = await CreateJobAsync(client, provider.ServiceCategoryId);
        int proposalId = await SubmitAsync(serviceId, provider, amount);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{serviceId}/proposals/{proposalId}/accept");
        return serviceId;
    }

    // The creation date comes from the clock of the API, so the services of other months are moved in the database.
    private async Task SetCreationDateAsync(int serviceId, DateTimeOffset creationDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Service\" SET \"creationDate\" = {creationDate} WHERE \"serviceId\" = {serviceId}");
    }
}