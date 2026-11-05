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

// F007 and F008, client side: E28 (accept a proposal), E29 (decline a proposal), E21 (my services), E22 (a service as
// the client sees it, with its direct requests and proposals) and E23 (reserved to providers). The 401 cases are in
// AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ClientProposalsEndpointsTests(LocalHubApiFactory factory)
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string InvalidTransitionType = "https://localhub.local/erros/transicao-invalida";
    private const string InactiveProviderType = "https://localhub.local/erros/prestador-inativo";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E28: criterion 1 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-03")]
    public async Task AcceptProposal_TresPropostasPendentes_AtribuiOServicoRecusaAsOutrasEAnulaAsSolicitacoes()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 3, silentProviders: 1);
        int accepted = scenario.ProposalIds[0];

        using var response = await PostAsync(scenario.Client.Token, AcceptUrl(accepted));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(accepted, decision.GetProperty("proposalId").GetInt32());
        Assert.Equal("ACCEPTED", decision.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, decision.GetProperty("decisionDate").ValueKind);

        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal("ASSIGNED", service.GetProperty("status").GetString());

        // The provider that did not answer gets VOID; the others keep PROPOSAL_SUBMITTED.
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => ProfileIdOf(r), r => r.GetProperty("status").GetString());
        Assert.Equal("VOID", requests[scenario.Providers[3].ProfessionalProfileId]);
        Assert.All(scenario.Providers.Take(3), p => Assert.Equal("PROPOSAL_SUBMITTED", requests[p.ProfessionalProfileId]));

        var proposals = await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId);
        var states = proposals.ToDictionary(p => p.GetProperty("proposalId").GetInt32(), p => p);
        Assert.Equal("ACCEPTED", states[accepted].GetProperty("status").GetString());
        Assert.All(scenario.ProposalIds.Skip(1), id =>
        {
            Assert.Equal("DECLINED", states[id].GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, states[id].GetProperty("decisionDate").ValueKind);
        });
    }

    [Fact]
    [Trait("Requisito", "RF-F008-02")]
    public async Task AcceptProposal_TrabalhoPublicado_Devolve200EServicoFicaAtribuido()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);

        using var response = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ProposalIds[0]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["proposalId", "status", "decisionDate"], decision.EnumerateObject().Select(f => f.Name));
        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal("ASSIGNED", service.GetProperty("status").GetString());
        Assert.Equal("ACCEPTED", Assert.Single(service.GetProperty("proposals").EnumerateArray()).GetProperty("status").GetString());
        Assert.Empty(service.GetProperty("directRequests").EnumerateArray());
        Assert.False(service.GetProperty("reviewed").GetBoolean());
        var change = Assert.Single(service.GetProperty("history").EnumerateArray());
        Assert.Equal("OPEN", change.GetProperty("previousStatus").GetString());
        Assert.Equal("ASSIGNED", change.GetProperty("newStatus").GetString());
        Assert.Equal("CLIENT", change.GetProperty("changedByRole").GetString());
    }

    // ---------- E28: criteria 2 and 5 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    public async Task AcceptProposal_SegundaPropostaNoMesmoServico_Devolve409TransicaoInvalida()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);
        using var first = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ProposalIds[0]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        string url = AcceptUrl(scenario.ProposalIds[1]);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
        Assert.Equal(1, await CountAcceptedAsync(scenario.ServiceId));
    }

    [Fact]
    [Trait("Requisito", "RF-F008-06")]
    public async Task AcceptProposal_PropostaRecusada_Devolve409TransicaoInvalidaEServicoFicaAberto()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        int proposalId = scenario.ProposalIds[0];
        using var declined = await PostAsync(scenario.Client.Token, DeclineUrl(proposalId));
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);

        string url = AcceptUrl(proposalId);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
        Assert.Equal("OPEN", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    public async Task AcceptProposal_ServicoCancelado_Devolve409TransicaoInvalida()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        await CancelServiceAsync(scenario.ServiceId);

        string url = AcceptUrl(scenario.ProposalIds[0]);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
    }

    // RF-F002-02, rule 7.
    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    [Trait("Requisito", "RF-F007-03")]
    public async Task AcceptProposal_PrestadorComContaDesativada_Devolve409PrestadorInativoEServicoFicaAberto()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        await DeactivateAsync(scenario.Providers[0].UserId);

        string url = AcceptUrl(scenario.ProposalIds[0]);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InactiveProviderType, url);
        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal("PENDING", Assert.Single(service.GetProperty("proposals").EnumerateArray()).GetProperty("status").GetString());
    }

    // ---------- E29: criterion 3 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-04")]
    [Trait("Requisito", "RF-F008-03")]
    public async Task DeclineProposal_PropostaPendente_Devolve200ERestoNaoMuda()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 2, silentProviders: 1);
        int declined = scenario.ProposalIds[0];

        using var response = await PostAsync(scenario.Client.Token, DeclineUrl(declined));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["proposalId", "status", "decisionDate"], proposal.EnumerateObject().Select(f => f.Name));
        Assert.Equal(declined, proposal.GetProperty("proposalId").GetInt32());
        Assert.Equal("DECLINED", proposal.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, proposal.GetProperty("decisionDate").ValueKind);

        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => ProfileIdOf(r), r => r.GetProperty("status").GetString());
        Assert.Equal("PENDING", requests[scenario.Providers[2].ProfessionalProfileId]);
        var other = (await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId))
            .Single(p => p.GetProperty("proposalId").GetInt32() == scenario.ProposalIds[1]);
        Assert.Equal("PENDING", other.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("DECLINED")]
    [InlineData("ACCEPTED")]
    [Trait("Requisito", "RF-F008-06")]
    public async Task DeclineProposal_PropostaJaDecidida_Devolve409TransicaoInvalida(string state)
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        int proposalId = scenario.ProposalIds[0];
        string first = state == "DECLINED" ? DeclineUrl(proposalId) : AcceptUrl(proposalId);
        using var decided = await PostAsync(scenario.Client.Token, first);
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);

        string url = DeclineUrl(proposalId);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
    }

    // ---------- E28 and E29: criterion 4 ----------

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    [Trait("Requisito", "RF-F007-08")]
    [Trait("Requisito", "RNF-06")]
    public async Task Decisao_ClienteQueNaoEAutor_Devolve403ENaoAlteraAProposta(string action)
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        var other = await RegisterClientAsync(_client);

        string url = $"{ProposalsUrl}/{scenario.ProposalIds[0]}/{action}";
        using var response = await PostAsync(other.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
        var proposal = Assert.Single(await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId));
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    [Trait("Requisito", "RF-F007-08")]
    public async Task Decisao_Prestador_Devolve403(string action)
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);

        string url = $"{ProposalsUrl}/{scenario.ProposalIds[0]}/{action}";
        using var response = await PostAsync(scenario.Providers[0].Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    [Trait("Requisito", "RF-F007-03")]
    public async Task Decisao_PropostaInexistente_Devolve404(string action)
    {
        var client = await RegisterClientAsync(_client);

        string url = $"{ProposalsUrl}/{int.MaxValue}/{action}";
        using var response = await PostAsync(client.Token, url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    // ---------- E21: criterion 6 ----------

    [Fact]
    [Trait("Requisito", "RF-F008-01")]
    public async Task ListMine_Cliente_DevolveSoOsSeusServicosDoMaisRecenteComPropostasPendentes()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);
        int newest = await CreatePublicServiceAsync(scenario.Client.Token, scenario.Providers[0].ServiceCategoryId);
        await CreatePublicScenarioAsync(proposals: 0); // a service of another client

        using var response = await GetAsync(scenario.Client.Token, ServicesUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([newest, scenario.ServiceId], items.Select(i => i.GetProperty("serviceId").GetInt32()));
        Assert.Equal([0, 2], items.Select(i => i.GetProperty("pendingProposalsCount").GetInt32()));
        Assert.Equal([0, 2], items.Select(i => i.GetProperty("proposalsCount").GetInt32()));
        Assert.Equal([0, 0], items.Select(i => i.GetProperty("directRequestsCount").GetInt32()));
        Assert.All(items, i => Assert.Equal(JsonValueKind.Null, i.GetProperty("assignedProvider").ValueKind));
        Assert.All(items, i => Assert.False(i.GetProperty("reviewed").GetBoolean()));
        Assert.Equal("PUBLIC", items[1].GetProperty("proposalModel").GetString());
        Assert.False(items[1].TryGetProperty("directRequests", out _));
        // Only the summary of the lists: the detail and the coordinates are not returned (API documentation, E21).
        Assert.False(items[1].TryGetProperty("description", out _));
        Assert.False(items[1].TryGetProperty("latitude", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F008-01")]
    public async Task ListMine_PropostaAceite_DevolveOPrestadorAtribuidoEOsContadores()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 2, silentProviders: 1);
        using var accept = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ProposalIds[0]));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        using var response = await GetAsync(scenario.Client.Token, ServicesUrl);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("ASSIGNED", item.GetProperty("status").GetString());
        Assert.Equal(3, item.GetProperty("directRequestsCount").GetInt32());
        Assert.Equal(2, item.GetProperty("proposalsCount").GetInt32());
        Assert.Equal(0, item.GetProperty("pendingProposalsCount").GetInt32());
        var assigned = item.GetProperty("assignedProvider");
        Assert.Equal(scenario.Providers[0].ProfessionalProfileId, assigned.GetProperty("professionalProfileId").GetInt32());
        Assert.Equal("Prestador Teste", assigned.GetProperty("name").GetString());
    }

    // E21 and E22 are only for the client: the provider follows its services with E23, E27, E36 and E37.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requisito", "RF-F008-01")]
    public async Task ListMineEGetService_Prestador_Devolve403(bool detail)
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        string url = detail ? $"{ServicesUrl}/{scenario.ServiceId}" : ServicesUrl;

        using var response = await GetAsync(scenario.Providers[0].Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-01")]
    public async Task ListMine_FiltrosEPaginacao_DevolveOsServicosQueCorrespondem()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        int published = await CreatePublicServiceAsync(client.Token, provider.ServiceCategoryId);
        int direct = await CreateDirectServiceAsync(client.Token, provider.ServiceCategoryId, provider.ProfessionalProfileId);
        await CancelServiceAsync(published);

        using var byStatus = await GetAsync(client.Token, $"{ServicesUrl}?status=CANCELLED");
        using var secondPage = await GetAsync(client.Token, $"{ServicesUrl}?page=2&pageSize=1");
        using var firstPage = await GetAsync(client.Token, $"{ServicesUrl}?page=1&pageSize=1");

        var first = await firstPage.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(direct, Assert.Single(first.GetProperty("items").EnumerateArray()).GetProperty("serviceId").GetInt32());
        var status = await byStatus.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(published, Assert.Single(status.GetProperty("items").EnumerateArray()).GetProperty("serviceId").GetInt32());
        var paged = await secondPage.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, paged.GetProperty("total").GetInt32());
        Assert.Equal(published, Assert.Single(paged.GetProperty("items").EnumerateArray()).GetProperty("serviceId").GetInt32());
    }

    [Theory]
    [InlineData("?status=INVALID", "status")]
    [InlineData("?pageSize=51", "pageSize")]
    [Trait("Requisito", "RF-F008-01")]
    public async Task ListMine_FiltroInvalido_Devolve400(string query, string field)
    {
        var client = await RegisterClientAsync(_client);

        using var response = await GetAsync(client.Token, $"{ServicesUrl}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ServicesUrl);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    // ---------- E22: criterion 7 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RF-F008-01")]
    public async Task GetService_ClienteAutor_VeTodasAsSolicitacoesComOPrestadorEAProposta()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 1, silentProviders: 1);

        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);

        Assert.Equal("Cliente Teste", service.GetProperty("clientName").GetString());
        Assert.False(service.TryGetProperty("clientId", out _));
        Assert.Equal("DIRECT_REQUEST", service.GetProperty("proposalModel").GetString());
        Assert.Empty(service.GetProperty("proposals").EnumerateArray());
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(r => ProfileIdOf(r));
        Assert.Equal(2, requests.Count);
        Assert.All(requests.Values, r =>
        {
            var profile = r.GetProperty("professionalProfile");
            Assert.Equal("Prestador Teste", profile.GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, profile.GetProperty("averageRating").ValueKind);
            Assert.Equal(0, profile.GetProperty("reviewCount").GetInt32());
        });
        var proposal = requests[scenario.Providers[0].ProfessionalProfileId].GetProperty("proposal");
        Assert.Equal(scenario.ProposalIds[0], proposal.GetProperty("proposalId").GetInt32());
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
        Assert.Equal(80.00m, proposal.GetProperty("amount").GetDecimal());
        Assert.Equal(JsonValueKind.Null,
            requests[scenario.Providers[1].ProfessionalProfileId].GetProperty("proposal").ValueKind);
        Assert.Empty(service.GetProperty("history").EnumerateArray());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RF-F008-01")]
    public async Task GetService_TrabalhoPublicado_DevolveTodasAsPropostasComOPrestadorDaMaisRecente()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);
        using var declined = await PostAsync(scenario.Client.Token, DeclineUrl(scenario.ProposalIds[0]));
        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);

        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);

        Assert.Empty(service.GetProperty("directRequests").EnumerateArray());
        var proposals = service.GetProperty("proposals").EnumerateArray().ToList();
        Assert.Equal([scenario.ProposalIds[1], scenario.ProposalIds[0]], proposals.Select(p => p.GetProperty("proposalId").GetInt32()));
        Assert.Equal(["PENDING", "DECLINED"], proposals.Select(p => p.GetProperty("status").GetString()));
        var proposal = proposals[0];
        Assert.Equal(80.00m, proposal.GetProperty("amount").GetDecimal());
        Assert.Equal(scenario.Providers[1].ProfessionalProfileId,
            proposal.GetProperty("professionalProfile").GetProperty("professionalProfileId").GetInt32());
        Assert.Equal("Prestador Teste", proposal.GetProperty("professionalProfile").GetProperty("name").GetString());
        Assert.Equal(0, proposal.GetProperty("professionalProfile").GetProperty("reviewCount").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RNF-06")]
    public async Task GetService_OutroCliente_Devolve403()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        var other = await RegisterClientAsync(_client);

        using var response = await GetAsync(other.Token, $"{ServicesUrl}/{scenario.ServiceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, $"{ServicesUrl}/{scenario.ServiceId}");
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task GetService_ServicoInexistente_Devolve404()
    {
        var client = await RegisterClientAsync(_client);

        using var response = await GetAsync(client.Token, $"{ServicesUrl}/{int.MaxValue}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- E23: only providers ----------

    // The client sees the direct requests of each service in E22 (API documentation, E23).
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task ListDirectRequests_Cliente_Devolve403()
    {
        var client = await RegisterClientAsync(_client);

        using var response = await GetAsync(client.Token, "/api/direct-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, "/api/direct-requests");
    }

    // ---------- Helpers ----------

    // A service of a client and the providers in its category; the first "proposals" providers submitted a proposal.
    private sealed record Scenario(TestClient Client, int ServiceId, List<ProviderWithProfile> Providers, List<int> ProposalIds);

    private const string ProposalsUrl = "/api/proposals";

    private static string AcceptUrl(int proposalId) => $"{ProposalsUrl}/{proposalId}/accept";

    private static string DeclineUrl(int proposalId) => $"{ProposalsUrl}/{proposalId}/decline";

    private static int ProfileIdOf(JsonElement directRequest) =>
        directRequest.GetProperty("professionalProfile").GetProperty("professionalProfileId").GetInt32();

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    private Task<HttpResponseMessage> PostAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Post, url, token);

    private async Task<JsonElement> GetServiceAsync(string token, int serviceId)
    {
        using var response = await GetAsync(token, $"{ServicesUrl}/{serviceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // All the proposals of the service, as E22 returns them: in "proposals" (PUBLIC) or in each direct request.
    private async Task<List<JsonElement>> ListProposalsAsync(string token, int serviceId)
    {
        var service = await GetServiceAsync(token, serviceId);
        var proposals = service.GetProperty("proposals").EnumerateArray().ToList();
        proposals.AddRange(service.GetProperty("directRequests").EnumerateArray()
            .Select(r => r.GetProperty("proposal"))
            .Where(p => p.ValueKind != JsonValueKind.Null));
        return proposals;
    }

    // A service with direct requests to "proposals + silentProviders" providers; the first ones submit a proposal.
    private async Task<Scenario> CreateDirectScenarioAsync(int proposals, int silentProviders)
    {
        var providers = await CreateProvidersAsync(proposals + silentProviders);
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreateDirectServiceAsync(
            client.Token, providers[0].ServiceCategoryId, [.. providers.Select(p => p.ProfessionalProfileId)]);
        return new Scenario(client, serviceId, providers, await SubmitAllAsync(serviceId, providers.Take(proposals)));
    }

    // A published job; the first "proposals" providers of the category submit a proposal.
    private async Task<Scenario> CreatePublicScenarioAsync(int proposals)
    {
        var providers = await CreateProvidersAsync(Math.Max(proposals, 1));
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreatePublicServiceAsync(client.Token, providers[0].ServiceCategoryId);
        return new Scenario(client, serviceId, providers, await SubmitAllAsync(serviceId, providers.Take(proposals)));
    }

    // The first provider creates its own category; the others are created in that category.
    private async Task<List<ProviderWithProfile>> CreateProvidersAsync(int count)
    {
        var first = await CreateProviderWithProfileAsync(_client);
        var providers = new List<ProviderWithProfile> { first };
        for (int i = 1; i < count; i++)
        {
            var (token, userId, email) = await RegisterProviderAsync(_client);
            using var response = await SendJsonAsync(
                _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(first.ServiceCategoryId));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
            providers.Add(new ProviderWithProfile(
                token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), first.ServiceCategoryId));
        }

        return providers;
    }

    private async Task<List<int>> SubmitAllAsync(int serviceId, IEnumerable<ProviderWithProfile> providers)
    {
        var ids = new List<int>();
        foreach (var provider in providers)
        {
            using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
                provider.Token, new { amount = 80.00m, conditions = "Material incluído." });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32());
        }

        return ids;
    }

    private async Task<int> CreateDirectServiceAsync(string clientToken, int serviceCategoryId, params int[] profileIds)
    {
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, clientToken, DirectRequestBody(serviceCategoryId, profileIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task<int> CreatePublicServiceAsync(string clientToken, int serviceCategoryId)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, clientToken, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task DeactivateAsync(int userId)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        using var response = await PostAsync(adminToken, $"/api/accounts/{userId}/deactivate");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The cancellation of a service is a later issue (I049), so the state is changed in the database.
    private async Task CancelServiceAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Service\" SET \"status\" = 'CANCELLED' WHERE \"serviceId\" = {serviceId}");
    }

    private async Task<int> CountAcceptedAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Proposals.CountAsync(
            p => p.ServiceId == serviceId && p.Status == LocalHub.Domain.Services.ProposalStatus.Accepted);
    }
}