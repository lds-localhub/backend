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

namespace LocalHub.IntegrationTests.Reports;

// F013: E33 (activity report of the client) and F014: E34 (activity report of the provider). Each test uses a new client
// or provider, so the report only has its data. The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ReportsEndpointsTests(LocalHubApiFactory factory)
{
    private const string ReportUrl = "/api/reports/client";
    private const string ProviderReportUrl = "/api/reports/provider";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";

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
        Assert.Equal(provider.ServiceCategoryId, item.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(categoryName, item.GetProperty("serviceCategory").GetProperty("name").GetString());
        Assert.Equal("Prestador Teste", item.GetProperty("providerName").GetString());
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

    // Criteria 1 and 3: the day of creation is the day in UTC (API documentation v04, section 4.3), so a service created
    // at 2026-09-30T23:30Z belongs to September, although it is already 1 October in Portugal.
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public async Task Relatorio_PeriodoDeOutubro_SoContaOsServicosCriadosNesseMesEmUtc()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int september = await CreateContractedAsync(client, provider, 10.00m);
        int firstOfOctober = await CreateContractedAsync(client, provider, 20.00m);
        int lastOfOctober = await CreateContractedAsync(client, provider, 40.00m);
        int november = await CreateContractedAsync(client, provider, 80.00m);
        await SetCreationDateAsync(september, new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero));
        await SetCreationDateAsync(firstOfOctober, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await SetCreationDateAsync(lastOfOctober, new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        await SetCreationDateAsync(november, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        var october = await GetReportAsync(client.Token, "?startDate=2026-10-01&endDate=2026-10-31");
        var all = await GetReportAsync(client.Token);

        Assert.Equal(2, october.GetProperty("contractedServices").GetInt32());
        Assert.Equal(60.00m, october.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Equal(4, all.GetProperty("contractedServices").GetInt32());
        Assert.False(october.TryGetProperty("period", out _));
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

    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public async Task Relatorio_SoComDataFinal_ContaOsServicosAteEssaData()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int old = await CreateContractedAsync(client, provider, 10.00m);
        await CreateContractedAsync(client, provider, 20.00m);
        await SetCreationDateAsync(old, new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        var report = await GetReportAsync(client.Token, "?endDate=2026-01-31");

        Assert.Equal(1, report.GetProperty("contractedServices").GetInt32());
        Assert.Equal(10.00m, report.GetProperty("totalAcceptedAmount").GetDecimal());
    }

    // The first and the last days that the dates can have: no 500 (before, endDate=9999-12-31 threw
    // ArgumentOutOfRangeException when the day after was calculated).
    [Theory]
    [Trait("Requisito", "RF-F013-02")]
    [InlineData("?endDate=9999-12-31")]
    [InlineData("?startDate=0001-01-01&endDate=9999-12-31")]
    public async Task Relatorio_DatasExtremas_Devolve200ComTodaAAtividade(string query)
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedAsync(client, provider, 80.00m);

        var report = await GetReportAsync(client.Token, query);

        Assert.Equal(1, report.GetProperty("contractedServices").GetInt32());
    }

    // Criterion 2: a start date after the end date is a global error of the operation, only in "detail".
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public async Task Relatorio_DataInicialPosteriorAFinal_Devolve400ComErroGlobal()
    {
        var client = await RegisterClientAsync(_client, withLocation: false);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, ReportUrl + "?startDate=2026-10-31&endDate=2026-10-01", client.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ReportUrl);
        Assert.Equal("A data inicial não pode ser posterior à data final.", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    // An invalid format is an error of the parameter, in "errors" (API documentation v04, section 4.6).
    [Theory]
    [Trait("Requisito", "RF-F013-02")]
    [InlineData("?startDate=2026-13-01", "startDate")]
    [InlineData("?endDate=01-10-2026", "endDate")]
    [InlineData("?startDate=2026-10-01&endDate=ontem", "endDate")]
    public async Task Relatorio_DataInvalida_Devolve400ComOErroNoParametro(string query, string field)
    {
        var client = await RegisterClientAsync(_client, withLocation: false);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReportUrl + query, client.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ReportUrl);
        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal(field, error.Name);
        Assert.Equal("Indique uma data válida no formato AAAA-MM-DD.", error.Value[0].GetString());
    }

    // ---------- E34, RF-F014-01: rules 1 to 4 ----------

    // Criterion 1: one accepted and three not chosen because the client accepted another provider; a PENDING proposal, a
    // VOID proposal and the proposals of the other provider are not counted.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_UmaAceiteETresNaoEscolhidas_ContaAsAceitesERecusadas()
    {
        var provider = await CreateProviderAsync();
        var other = await CreateProviderInCategoriesAsync(provider.ServiceCategoryId);
        var client = await RegisterClientAsync(_client);
        await CreateContractedAsync(client, provider, 80.00m);
        for (int i = 0; i < 3; i++)
        {
            int declinedJob = await CreateJobAsync(client, provider.ServiceCategoryId);
            await SubmitAsync(declinedJob, provider, 90.00m);
            int chosen = await SubmitAsync(declinedJob, other, 70.00m);
            await PostOkAsync(client.Token, $"{ServicesUrl}/{declinedJob}/proposals/{chosen}/accept");
        }

        int pending = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(pending, provider, 500.00m);
        int voided = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(voided, provider, 500.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{voided}/cancel");

        var report = await GetProviderReportAsync(provider.Token);

        var proposals = report.GetProperty("proposals");
        Assert.Equal(1, proposals.GetProperty("accepted").GetInt32());
        Assert.Equal(3, proposals.GetProperty("declined").GetInt32());
        Assert.Equal(80.00m, report.GetProperty("totalAcceptedAmount").GetDecimal());
    }

    // Rule 1: only the COMPLETED ones are completed services. Rule 2: the distributions have all the services in which
    // the proposal of the provider was accepted, and not the service that the client gave to another provider.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_ServicosComPropostaAceite_ContaRealizadosEDistribuiPorCategoriaEEstado()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int plumbing = await CreateCategoryAsync(_client, adminToken, $"Canalização {Guid.NewGuid():N}");
        int gardening = await CreateCategoryAsync(_client, adminToken, $"Jardinagem {Guid.NewGuid():N}");
        var provider = await CreateProviderInCategoriesAsync(plumbing, gardening);
        var other = await CreateProviderInCategoriesAsync(plumbing);
        var client = await RegisterClientAsync(_client);
        int completed = await CreateContractedAsync(client, provider with { ServiceCategoryId = plumbing }, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{completed}/complete");
        int cancelled = await CreateContractedAsync(client, provider with { ServiceCategoryId = plumbing }, 150.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelled}/cancel");
        await CreateContractedAsync(client, provider with { ServiceCategoryId = gardening }, 20.00m);
        await CreateContractedAsync(client, other, 999.00m);

        var report = await GetProviderReportAsync(provider.Token);

        Assert.Equal(1, report.GetProperty("completedServices").GetInt32());
        Assert.Equal(250.00m, report.GetProperty("totalAcceptedAmount").GetDecimal());
        var byCategory = report.GetProperty("byCategory").EnumerateArray().ToList();
        Assert.Equal(2, byCategory.Count);
        Assert.Equal(plumbing, byCategory[0].GetProperty("serviceCategoryId").GetInt32());
        Assert.StartsWith("Canalização", byCategory[0].GetProperty("name").GetString());
        Assert.Equal(2, byCategory[0].GetProperty("count").GetInt32());
        Assert.Equal(gardening, byCategory[1].GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(1, byCategory[1].GetProperty("count").GetInt32());
        var byStatus = report.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("status").GetString()!, s => s.GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["ASSIGNED"] = 1, ["COMPLETED"] = 1, ["CANCELLED"] = 1 }, byStatus);
    }

    // Criterion 5: one pending and one declined; a direct request sent to another provider is not counted.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_DuasSolicitacoesDiretas_ContaEmQualquerEstado()
    {
        var provider = await CreateProviderAsync();
        var other = await CreateProviderInCategoriesAsync(provider.ServiceCategoryId);
        var client = await RegisterClientAsync(_client);
        await CreateDirectRequestAsync(client, provider);
        int declined = await CreateDirectRequestAsync(client, provider);
        await PostOkAsync(provider.Token, $"/api/direct-requests/{declined}/decline");
        await CreateDirectRequestAsync(client, other);

        var report = await GetProviderReportAsync(provider.Token);

        Assert.Equal(2, report.GetProperty("directRequestsReceived").GetInt32());
    }

    // ---------- E34, RF-F014-01: rule 5 ----------

    // Criterion 3 with the real clock of the API: each month has the mean of its reviews, and the month in the middle,
    // without reviews, has no value.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_AvaliacoesEmDoisMeses_ApresentaAMediaDeCadaMesAteAoMesAtual()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        var now = DateTimeOffset.UtcNow;
        await CreateReviewedAsync(client, provider, 5, MiddleOfMonth(now.AddMonths(-2)));
        await CreateReviewedAsync(client, provider, 3, MiddleOfMonth(now));

        var report = await GetProviderReportAsync(provider.Token);

        var evolution = report.GetProperty("ratingEvolution").EnumerateArray().ToList();
        Assert.Equal(3, evolution.Count);
        Assert.Equal(MonthText(now.AddMonths(-2)), evolution[0].GetProperty("month").GetString());
        Assert.Equal(1, evolution[0].GetProperty("reviewCount").GetInt32());
        Assert.Equal(5.0m, evolution[0].GetProperty("averageRating").GetDecimal());
        Assert.Equal(MonthText(now.AddMonths(-1)), evolution[1].GetProperty("month").GetString());
        Assert.Equal(0, evolution[1].GetProperty("reviewCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, evolution[1].GetProperty("averageRating").ValueKind);
        Assert.Equal(MonthText(now), evolution[2].GetProperty("month").GetString());
        Assert.Equal(3.0m, evolution[2].GetProperty("averageRating").GetDecimal());
    }

    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_SemAtividade_DevolveZerosEListasVazias()
    {
        var provider = await CreateProviderAsync();

        var report = await GetProviderReportAsync(provider.Token);

        Assert.Equal(0, report.GetProperty("completedServices").GetInt32());
        Assert.Empty(report.GetProperty("byCategory").EnumerateArray());
        Assert.All(report.GetProperty("byStatus").EnumerateArray(), s => Assert.Equal(0, s.GetProperty("count").GetInt32()));
        Assert.Equal(3, report.GetProperty("byStatus").GetArrayLength());
        Assert.Equal(0, report.GetProperty("directRequestsReceived").GetInt32());
        Assert.Equal(0, report.GetProperty("proposals").GetProperty("accepted").GetInt32());
        Assert.Equal(0, report.GetProperty("proposals").GetProperty("declined").GetInt32());
        Assert.Equal(0m, report.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Empty(report.GetProperty("ratingEvolution").EnumerateArray());
    }

    // ---------- E34, RF-F014-01: rule 6 ----------

    // Criteria 2 and 4: the route has no parameter to choose the provider, and the data of another one is never counted.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_SoIncluiOsDadosDoProprioPrestador()
    {
        var other = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateReviewedAsync(client, other, 5, DateTimeOffset.UtcNow);
        var provider = await CreateProviderInCategoriesAsync(other.ServiceCategoryId);

        var report = await GetProviderReportAsync(provider.Token);

        Assert.Equal(0, report.GetProperty("completedServices").GetInt32());
        Assert.Equal(0m, report.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Empty(report.GetProperty("ratingEvolution").EnumerateArray());
    }

    [Theory]
    [Trait("Requisito", "RF-F014-01")]
    [InlineData("CLIENT")]
    [InlineData("ADMINISTRATOR")]
    public async Task RelatorioPrestador_PapelDiferenteDePrestador_Devolve403(string role)
    {
        string token = role == "ADMINISTRATOR"
            ? await LoginAsAdminAsync(_client)
            : await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ProviderReportUrl, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task RelatorioPrestador_SemPerfil_Devolve403PerfilEmFalta()
    {
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ProviderReportUrl, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, ProviderReportUrl);
    }

    // ---------- E34, RF-F014-02 ----------

    // Criterion 1 and rule 2: the direct requests, the proposals and the accepted amounts follow the creation date of
    // the service, in UTC, although they were created later.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public async Task RelatorioPrestador_PeriodoDeOutubro_SoContaOsServicosCriadosNesseMesEOQueDelesDepende()
    {
        var provider = await CreateProviderAsync();
        var other = await CreateProviderInCategoriesAsync(provider.ServiceCategoryId);
        var client = await RegisterClientAsync(_client);

        int september = await CreateContractedAsync(client, provider, 10.00m);
        int firstOfOctober = await CreateContractedAsync(client, provider, 20.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{firstOfOctober}/complete");
        int november = await CreateContractedAsync(client, provider, 40.00m);

        int declinedInOctober = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(declinedInOctober, provider, 90.00m);
        int chosen = await SubmitAsync(declinedInOctober, other, 70.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{declinedInOctober}/proposals/{chosen}/accept");
        int declinedInSeptember = await CreateJobAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(declinedInSeptember, provider, 90.00m);
        chosen = await SubmitAsync(declinedInSeptember, other, 70.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{declinedInSeptember}/proposals/{chosen}/accept");

        int directInOctober = await CreateDirectServiceAsync(client, provider);
        int directInSeptember = await CreateDirectServiceAsync(client, provider);

        var lastOfSeptember = new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
        var firstDay = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        await SetCreationDateAsync(september, lastOfSeptember);
        await SetCreationDateAsync(firstOfOctober, firstDay);
        await SetCreationDateAsync(november, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        await SetCreationDateAsync(declinedInOctober, new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        await SetCreationDateAsync(declinedInSeptember, lastOfSeptember);
        await SetCreationDateAsync(directInOctober, firstDay);
        await SetCreationDateAsync(directInSeptember, lastOfSeptember);

        var october = await GetProviderReportAsync(provider.Token, "?startDate=2026-10-01&endDate=2026-10-31");
        var all = await GetProviderReportAsync(provider.Token);

        Assert.Equal(1, october.GetProperty("completedServices").GetInt32());
        Assert.Equal(1, october.GetProperty("byCategory")[0].GetProperty("count").GetInt32());
        Assert.Equal(1, october.GetProperty("directRequestsReceived").GetInt32());
        Assert.Equal(1, october.GetProperty("proposals").GetProperty("accepted").GetInt32());
        Assert.Equal(1, october.GetProperty("proposals").GetProperty("declined").GetInt32());
        Assert.Equal(20.00m, october.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Equal(3, all.GetProperty("proposals").GetProperty("accepted").GetInt32());
        Assert.Equal(2, all.GetProperty("proposals").GetProperty("declined").GetInt32());
        Assert.Equal(2, all.GetProperty("directRequestsReceived").GetInt32());
        Assert.False(october.TryGetProperty("period", out _));
    }

    // Criterion 3: the evolution depends on the month of the review, not on the creation date of the service, and the
    // mean of the month shown does not include the previous months.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public async Task RelatorioPrestador_PeriodoDeUmMes_ApresentaApenasEsseMes()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        var now = DateTimeOffset.UtcNow;
        await CreateReviewedAsync(client, provider, 5, MiddleOfMonth(now.AddMonths(-2)));
        await CreateReviewedAsync(client, provider, 3, MiddleOfMonth(now.AddMonths(-1)));
        var month = now.AddMonths(-1);
        string lastDay = DateTime.DaysInMonth(month.Year, month.Month).ToString("00", CultureInfo.InvariantCulture);

        var report = await GetProviderReportAsync(
            provider.Token, $"?startDate={MonthText(month)}-01&endDate={MonthText(month)}-{lastDay}");

        var item = Assert.Single(report.GetProperty("ratingEvolution").EnumerateArray());
        Assert.Equal(MonthText(month), item.GetProperty("month").GetString());
        Assert.Equal(1, item.GetProperty("reviewCount").GetInt32());
        Assert.Equal(3.0m, item.GetProperty("averageRating").GetDecimal());
    }

    // The first and the last days that the dates can have: no 500 and no list with thousands of months.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public async Task RelatorioPrestador_DatasExtremas_Devolve200ComOsMesesComAvaliacoes()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateReviewedAsync(client, provider, 4, DateTimeOffset.UtcNow);

        var report = await GetProviderReportAsync(provider.Token, "?startDate=0001-01-01&endDate=9999-12-31");

        Assert.Equal(1, report.GetProperty("completedServices").GetInt32());
        Assert.Equal(1, report.GetProperty("ratingEvolution").GetArrayLength());
    }

    // Criterion 2: a start date after the end date is a global error of the operation, only in "detail".
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public async Task RelatorioPrestador_DataInicialPosteriorAFinal_Devolve400ComErroGlobal()
    {
        var provider = await CreateProviderAsync();

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, ProviderReportUrl + "?startDate=2026-10-31&endDate=2026-10-01", provider.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ProviderReportUrl);
        Assert.Equal("A data inicial não pode ser posterior à data final.", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    [Theory]
    [Trait("Requisito", "RF-F014-02")]
    [InlineData("?startDate=2026-13-01", "startDate")]
    [InlineData("?endDate=01-10-2026", "endDate")]
    public async Task RelatorioPrestador_DataInvalida_Devolve400ComOErroNoParametro(string query, string field)
    {
        var provider = await CreateProviderAsync();

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ProviderReportUrl + query, provider.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ProviderReportUrl);
        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal(field, error.Name);
    }

    // ---------- Helpers ----------

    private async Task<JsonElement> GetReportAsync(string token, string query = "")
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReportUrl + query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetProviderReportAsync(string token, string query = "")
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ProviderReportUrl + query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string MonthText(DateTimeOffset date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // The 15th, so that the month does not depend on the day of the current date.
    private static DateTimeOffset MiddleOfMonth(DateTimeOffset date) => new(date.Year, date.Month, 15, 12, 0, 0, TimeSpan.Zero);

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
        return await CreateProviderInCategoriesAsync(categoryId);
    }

    // A provider with a profile in existing categories; ServiceCategoryId is the first one.
    private async Task<ProviderWithProfile> CreateProviderInCategoriesAsync(params int[] categoryIds)
    {
        var (token, userId, email) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(categoryIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryIds[0]);
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

    // A contracted service, completed and reviewed by the client, with the review moved to the given date.
    private async Task CreateReviewedAsync(TestClient client, ProviderWithProfile provider, int rating, DateTimeOffset date)
    {
        int serviceId = await CreateContractedAsync(client, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{serviceId}/complete");
        using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/review",
            client.Token, new { rating });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Review\" SET \"creationDate\" = {date} WHERE \"serviceId\" = {serviceId}");
    }

    // A service with a direct request to the provider; returns the identifier of the direct request.
    private async Task<int> CreateDirectRequestAsync(TestClient client, ProviderWithProfile provider)
    {
        var service = await PostDirectServiceAsync(client, provider);
        return service.GetProperty("directRequests")[0].GetProperty("directRequestId").GetInt32();
    }

    // The same, returning the identifier of the service.
    private async Task<int> CreateDirectServiceAsync(TestClient client, ProviderWithProfile provider)
    {
        var service = await PostDirectServiceAsync(client, provider);
        return service.GetProperty("serviceId").GetInt32();
    }

    private async Task<JsonElement> PostDirectServiceAsync(TestClient client, ProviderWithProfile provider)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token,
            DirectRequestBody(provider.ServiceCategoryId, provider.ProfessionalProfileId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
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