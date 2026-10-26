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

// F007 and F011, provider side: E25 (submit a proposal), E24 (decline a direct request), E27 (my proposals), E23 (my
// direct requests) and E22 (a service as the provider sees it). The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ProposalsEndpointsTests(LocalHubApiFactory factory)
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";
    private const string InvalidTransitionType = "https://localhub.local/erros/transicao-invalida";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E25: criteria 1 and 2 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public async Task SubmitProposal_DestinatarioDeSolicitacaoPendente_Devolve201ESolicitacaoPassaAPropostaApresentada()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);

        using var response = await SubmitAsync(provider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = await response.Content.ReadFromJsonAsync<JsonElement>();
        int proposalId = proposal.GetProperty("proposalId").GetInt32();
        Assert.Equal($"{ServicesUrl}/{serviceId}/proposals/{proposalId}", response.Headers.Location?.OriginalString);
        Assert.Equal(serviceId, proposal.GetProperty("serviceId").GetInt32());
        Assert.Equal(provider.ProfessionalProfileId, proposal.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal(80.00m, proposal.GetProperty("amount").GetDecimal());
        Assert.Equal("Inclui deslocação e material.", proposal.GetProperty("conditions").GetString());
        Assert.Equal("Disponível dia 9 às 18h30.", proposal.GetProperty("obs").GetString());
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
        Assert.EndsWith("Z", proposal.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, proposal.GetProperty("decisionDate").ValueKind);

        using var list = await GetAsync(provider.Token, "/api/direct-requests");
        var item = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0];
        Assert.Equal(requests[provider.ProfessionalProfileId], item.GetProperty("directRequestId").GetInt32());
        Assert.Equal("PROPOSAL_SUBMITTED", item.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, item.GetProperty("responseDate").ValueKind);
        Assert.Equal(proposalId, item.GetProperty("proposal").GetProperty("proposalId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_TrabalhoPublicadoAberto_Devolve201ComPropostaPendenteESemObservacoes()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);

        using var response = await SubmitAsync(provider.Token, serviceId, new { amount = 150.00m, conditions = "Material incluído." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, proposal.GetProperty("obs").ValueKind);
    }

    // ---------- E25: criterion 3 (validation) ----------

    [Theory]
    [InlineData("amount", "0")]
    [InlineData("amount", "-10")]
    [InlineData("amount", "12.345")]
    [InlineData("amount", "100000.01")]
    [InlineData("amount", null)]
    [InlineData("conditions", null)]
    [InlineData("conditions", "1001")]
    [InlineData("obs", "501")]
    [Trait("Requisito", "RF-F007-01")]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_CampoInvalido_Devolve400NoCampoENaoCriaProposta(string field, string? value)
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        var body = ValidProposal();
        body[field] = field switch
        {
            "amount" when value is not null => decimal.Parse(value, CultureInfo.InvariantCulture),
            "conditions" or "obs" when value is not null => new string('a', int.Parse(value, CultureInfo.InvariantCulture)),
            _ => null,
        };

        using var response = await SubmitAsync(provider.Token, serviceId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, $"{ServicesUrl}/{serviceId}/proposals");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Esperado um erro em errors.{field}.");
        Assert.Equal(0, await CountProposalsAsync(serviceId));
    }

    // ---------- E25: criteria 4 and 5 (conflicts and access) ----------

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_SegundaPropostaDoMesmoPrestador_Devolve409()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        using var first = await SubmitAsync(provider.Token, serviceId, ValidProposal());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await SubmitAsync(provider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await AssertProblemAsync(second, ConflictType, $"{ServicesUrl}/{serviceId}/proposals");
        Assert.Equal(1, await CountProposalsAsync(serviceId));
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_ServicoNaoAberto_Devolve409TransicaoInvalida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        await CancelServiceAsync(serviceId);

        using var response = await SubmitAsync(provider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, $"{ServicesUrl}/{serviceId}/proposals");
        Assert.Equal(0, await CountProposalsAsync(serviceId));
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public async Task SubmitProposal_SolicitacaoRecusada_Devolve409TransicaoInvalida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        using var declined = await PostAsync(provider.Token, $"/api/direct-requests/{requests[provider.ProfessionalProfileId]}/decline");
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);

        using var response = await SubmitAsync(provider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, $"{ServicesUrl}/{serviceId}/proposals");
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_PrestadorSemACategoria_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);

        using var response = await SubmitAsync(other.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, $"{ServicesUrl}/{serviceId}/proposals");
    }

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public async Task SubmitProposal_PrestadorSemSolicitacaoDireta_Devolve403()
    {
        var recipient = await CreateProviderWithProfileAsync(_client);
        var outsider = await CreateProviderInCategoryAsync(recipient.ServiceCategoryId);
        var (_, serviceId, _) = await CreateDirectServiceAsync(recipient.ServiceCategoryId, recipient.ProfessionalProfileId);

        using var response = await SubmitAsync(outsider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, $"{ServicesUrl}/{serviceId}/proposals");
        Assert.Equal(0, await CountProposalsAsync(serviceId));
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_ClienteComContaDesativada_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (client, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        await DeactivateAsync(client.UserId);

        using var response = await SubmitAsync(provider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, $"{ServicesUrl}/{serviceId}/proposals");
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_Cliente_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (client, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);

        using var response = await SubmitAsync(client.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public async Task SubmitProposal_PrestadorSemPerfil_Devolve403PerfilEmFalta()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await SubmitAsync(token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, $"{ServicesUrl}/{serviceId}/proposals");
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_ServicoInexistente_Devolve404()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await SubmitAsync(provider.Token, int.MaxValue, ValidProposal());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, $"{ServicesUrl}/{int.MaxValue}/proposals");
    }

    // ---------- E24 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_DestinatarioDeSolicitacaoPendente_Devolve200RecusadaEServicoContinuaAberto()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        int requestId = requests[provider.ProfessionalProfileId];

        using var response = await PostAsync(provider.Token, $"/api/direct-requests/{requestId}/decline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(requestId, body.GetProperty("directRequestId").GetInt32());
        Assert.Equal(serviceId, body.GetProperty("serviceId").GetInt32());
        Assert.Equal(provider.ProfessionalProfileId, body.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal("DECLINED", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("responseDate").ValueKind);

        using var service = await GetAsync(provider.Token, $"{ServicesUrl}/{serviceId}");
        var seen = await service.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OPEN", seen.GetProperty("status").GetString());
        Assert.Equal("DECLINED", seen.GetProperty("directRequests")[0].GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-08")]
    public async Task Decline_SolicitacaoJaRecusada_Devolve409TransicaoInvalida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, _, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        string url = $"/api/direct-requests/{requests[provider.ProfessionalProfileId]}/decline";
        using var first = await PostAsync(provider.Token, url);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await PostAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await AssertProblemAsync(second, InvalidTransitionType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-08")]
    public async Task Decline_PropostaJaApresentada_Devolve409TransicaoInvalida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        using var submitted = await SubmitAsync(provider.Token, serviceId, ValidProposal());
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        using var response = await PostAsync(provider.Token, $"/api/direct-requests/{requests[provider.ProfessionalProfileId]}/decline");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_ServicoNaoAberto_Devolve409TransicaoInvalida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        await CancelServiceAsync(serviceId);

        using var response = await PostAsync(provider.Token, $"/api/direct-requests/{requests[provider.ProfessionalProfileId]}/decline");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_SolicitacaoDeOutroPrestador_Devolve403()
    {
        var recipient = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(recipient.ServiceCategoryId);
        var (_, _, requests) = await CreateDirectServiceAsync(recipient.ServiceCategoryId, recipient.ProfessionalProfileId);
        string url = $"/api/direct-requests/{requests[recipient.ProfessionalProfileId]}/decline";

        using var response = await PostAsync(other.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_SolicitacaoInexistente_Devolve404()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await PostAsync(provider.Token, $"/api/direct-requests/{int.MaxValue}/decline");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_Cliente_Devolve403()
    {
        var client = await RegisterClientAsync(_client);

        using var response = await PostAsync(client.Token, "/api/direct-requests/1/decline");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public async Task Decline_PrestadorSemPerfil_Devolve403PerfilEmFalta()
    {
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await PostAsync(token, "/api/direct-requests/1/decline");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, "/api/direct-requests/1/decline");
    }

    // ---------- E27 (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    public async Task ListMyProposals_VariasPropostas_DevolvePaginaOrdenadaDaMaisRecenteComResumoDoServico()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, firstService) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        var (_, secondService, _) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        using var first = await SubmitAsync(provider.Token, firstService, ValidProposal());
        using var second = await SubmitAsync(provider.Token, secondService, ValidProposal());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var response = await GetAsync(provider.Token, "/api/proposals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([secondService, firstService], items.Select(i => i.GetProperty("serviceId").GetInt32()));
        var summary = items[0].GetProperty("service");
        Assert.Equal(secondService, summary.GetProperty("serviceId").GetInt32());
        Assert.Equal("DIRECT_REQUEST", summary.GetProperty("proposalModel").GetString());
        Assert.Equal(provider.ServiceCategoryId, summary.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("OPEN", summary.GetProperty("status").GetString());
        Assert.False(summary.TryGetProperty("clientId", out _));

        using var secondPage = await GetAsync(provider.Token, "/api/proposals?page=2&pageSize=1");
        var paged = await secondPage.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, paged.GetProperty("total").GetInt32());
        Assert.Equal(firstService, paged.GetProperty("items")[0].GetProperty("serviceId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    public async Task ListMyProposals_ComFiltros_DevolveSoAsPropostasDoPrestadorQueCorrespondem()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, publicService) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        using var mine = await SubmitAsync(provider.Token, publicService, ValidProposal());
        using var others = await SubmitAsync(other.Token, publicService, ValidProposal());
        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Created, others.StatusCode);

        async Task<int> TotalAsync(string query)
        {
            using var response = await GetAsync(provider.Token, $"/api/proposals{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32();
        }

        Assert.Equal(1, await TotalAsync(string.Empty));
        Assert.Equal(1, await TotalAsync("?status=PENDING"));
        Assert.Equal(0, await TotalAsync("?status=ACCEPTED"));
        Assert.Equal(1, await TotalAsync("?proposalModel=PUBLIC"));
        Assert.Equal(0, await TotalAsync("?proposalModel=DIRECT_REQUEST"));
    }

    [Theory]
    [InlineData("?status=INVALID", "status")]
    [InlineData("?proposalModel=INVALID", "proposalModel")]
    [InlineData("?page=0", "page")]
    [InlineData("?pageSize=51", "pageSize")]
    [Trait("Requisito", "RF-F008-07")]
    public async Task ListMyProposals_FiltroInvalido_Devolve400(string query, string field)
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await GetAsync(provider.Token, $"/api/proposals{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, "/api/proposals");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    public async Task ListMyProposals_ClienteOuPrestadorSemPerfil_Devolve403()
    {
        var client = await RegisterClientAsync(_client);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var asClient = await GetAsync(client.Token, "/api/proposals");
        using var withoutProfile = await GetAsync(token, "/api/proposals");

        Assert.Equal(HttpStatusCode.Forbidden, asClient.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutProfile.StatusCode);
        await AssertProblemAsync(withoutProfile, MissingProfileType, "/api/proposals");
    }

    // ---------- E23 (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task ListDirectRequests_SolicitacoesRecebidas_DevolveSoAsDoPrestadorSemDadosDoCliente()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, firstService, _) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId, other.ProfessionalProfileId);
        var (_, secondService, secondRequests) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId);

        using var response = await GetAsync(provider.Token, "/api/direct-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([secondService, firstService], items.Select(i => i.GetProperty("service").GetProperty("serviceId").GetInt32()));
        Assert.Equal(secondRequests[provider.ProfessionalProfileId], items[0].GetProperty("directRequestId").GetInt32());
        Assert.Equal("PENDING", items[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("proposal").ValueKind);
        var service = items[0].GetProperty("service");
        Assert.Equal(provider.ServiceCategoryId, service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.DoesNotContain("clientId", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task ListDirectRequests_FiltroPorEstadoEPaginacao_DevolveAPaginaPedida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, _, firstRequests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        using var declined = await PostAsync(provider.Token, $"/api/direct-requests/{firstRequests[provider.ProfessionalProfileId]}/decline");
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);

        using var onlyDeclined = await GetAsync(provider.Token, "/api/direct-requests?status=DECLINED");
        using var secondPage = await GetAsync(provider.Token, "/api/direct-requests?page=2&pageSize=1");

        var filtered = await onlyDeclined.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
        Assert.Equal(firstRequests[provider.ProfessionalProfileId], filtered.GetProperty("items")[0].GetProperty("directRequestId").GetInt32());
        var paged = await secondPage.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, paged.GetProperty("total").GetInt32());
        Assert.Single(paged.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("?status=INVALID", "status")]
    [InlineData("?page=0", "page")]
    [Trait("Requisito", "RF-F007-09")]
    public async Task ListDirectRequests_FiltroInvalido_Devolve400(string query, string field)
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await GetAsync(provider.Token, $"/api/direct-requests{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, "/api/direct-requests");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task ListDirectRequests_ClienteOuPrestadorSemPerfil_Devolve403()
    {
        var client = await RegisterClientAsync(_client);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var asClient = await GetAsync(client.Token, "/api/direct-requests");
        using var withoutProfile = await GetAsync(token, "/api/direct-requests");

        Assert.Equal(HttpStatusCode.Forbidden, asClient.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutProfile.StatusCode);
        await AssertProblemAsync(withoutProfile, MissingProfileType, "/api/direct-requests");
    }

    // ---------- E22 (criterion 8) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetService_DestinatarioComProposta_VeSoOsSeusDadosEADistancia()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId, other.ProfessionalProfileId);
        using var submitted = await SubmitAsync(provider.Token, serviceId, ValidProposal());
        using var othersProposal = await SubmitAsync(other.Token, serviceId, ValidProposal());
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        Assert.Equal(HttpStatusCode.Created, othersProposal.StatusCode);

        using var response = await GetAsync(provider.Token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(serviceId, service.GetProperty("serviceId").GetInt32());
        Assert.Equal("DIRECT_REQUEST", service.GetProperty("proposalModel").GetString());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal(0m, service.GetProperty("distanceKm").GetDecimal());
        Assert.False(service.GetProperty("reviewed").GetBoolean());
        Assert.Empty(service.GetProperty("history").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("acceptedProposal").ValueKind);

        var directRequests = service.GetProperty("directRequests").EnumerateArray().ToList();
        var own = Assert.Single(directRequests);
        Assert.Equal(requests[provider.ProfessionalProfileId], own.GetProperty("directRequestId").GetInt32());
        Assert.Equal("PROPOSAL_SUBMITTED", own.GetProperty("status").GetString());
        var proposal = service.GetProperty("myProposal");
        Assert.Equal(80.00m, proposal.GetProperty("amount").GetDecimal());
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());

        // Nothing of the Client or of the other provider.
        Assert.False(service.TryGetProperty("clientId", out _));
        Assert.DoesNotContain("clientId", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain(requests[other.ProfessionalProfileId].ToString(CultureInfo.InvariantCulture),
            directRequests.Select(r => r.GetProperty("directRequestId").GetInt32().ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    public async Task GetService_TrabalhoPublicadoAbertoSemProposta_Devolve200ComMyProposalNulo()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);

        using var response = await GetAsync(provider.Token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PUBLIC", service.GetProperty("proposalModel").GetString());
        Assert.Empty(service.GetProperty("directRequests").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("myProposal").ValueKind);
        Assert.False(service.TryGetProperty("clientId", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    public async Task GetService_TrabalhoPublicadoNaoAberto_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        await CancelServiceAsync(serviceId);

        using var response = await GetAsync(provider.Token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, $"{ServicesUrl}/{serviceId}");
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    public async Task GetService_TrabalhoPublicadoDeClienteDesativado_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (client, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        await DeactivateAsync(client.UserId);

        using var response = await GetAsync(provider.Token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RNF-06")]
    public async Task GetService_SolicitacoesDiretasSemSolicitacaoAoPrestador_Devolve403()
    {
        var recipient = await CreateProviderWithProfileAsync(_client);
        var outsider = await CreateProviderInCategoryAsync(recipient.ServiceCategoryId);
        var (_, serviceId, _) = await CreateDirectServiceAsync(recipient.ServiceCategoryId, recipient.ProfessionalProfileId);

        using var response = await GetAsync(outsider.Token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, $"{ServicesUrl}/{serviceId}");
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task GetService_ServicoInexistente_Devolve404()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        using var response = await GetAsync(provider.Token, $"{ServicesUrl}/{int.MaxValue}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, $"{ServicesUrl}/{int.MaxValue}");
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public async Task GetService_PrestadorSemPerfil_Devolve403PerfilEmFalta()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        var (token, _, _) = await RegisterProviderAsync(_client);

        using var response = await GetAsync(token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, $"{ServicesUrl}/{serviceId}");
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    public async Task GetService_Cliente_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (client, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);

        using var response = await GetAsync(client.Token, $"{ServicesUrl}/{serviceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Helpers ----------

    private static Dictionary<string, object?> ValidProposal() => new()
    {
        ["amount"] = 80.00m,
        ["conditions"] = "Inclui deslocação e material.",
        ["obs"] = "Disponível dia 9 às 18h30.",
    };

    private Task<HttpResponseMessage> SubmitAsync(string token, int serviceId, object body) =>
        SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals", token, body);

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    private Task<HttpResponseMessage> PostAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Post, url, token);

    // A service with direct requests to the given profiles (E20). Returns the client, the serviceId and the
    // directRequestId of each profile.
    private async Task<(TestClient Client, int ServiceId, Dictionary<int, int> Requests)> CreateDirectServiceAsync(
        int serviceCategoryId, params int[] professionalProfileIds)
    {
        var client = await RegisterClientAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, DirectRequestBody(serviceCategoryId, professionalProfileIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => r.GetProperty("professionalProfileId").GetInt32(),
            r => r.GetProperty("directRequestId").GetInt32());
        return (client, service.GetProperty("serviceId").GetInt32(), requests);
    }

    private async Task<(TestClient Client, int ServiceId)> CreatePublicServiceAsync(int serviceCategoryId)
    {
        var client = await RegisterClientAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (client, service.GetProperty("serviceId").GetInt32());
    }

    // A second provider with a profile in the category.
    private async Task<ProviderWithProfile> CreateProviderInCategoryAsync(int serviceCategoryId)
    {
        var (token, userId, email) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(
            token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), serviceCategoryId);
    }

    private async Task DeactivateAsync(int userId)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        using var response = await PostAsync(adminToken, $"/api/accounts/{userId}/deactivate");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The endpoints that cancel or assign a service are later issues, so the state is changed in the database.
    private async Task CancelServiceAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Service\" SET \"status\" = 'CANCELLED' WHERE \"serviceId\" = {serviceId}");
    }

    private async Task<int> CountProposalsAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Proposals.CountAsync(p => p.ServiceId == serviceId);
    }
}