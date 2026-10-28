using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Services;

// F007 and F008: E30 (cancel a service) and E31 (mark a service as completed). The 401 cases are in
// AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ServiceLifecycleEndpointsTests(LocalHubApiFactory factory)
{
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string InvalidTransitionType = "https://localhub.local/erros/transicao-invalida";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E30: client, OPEN (criteria 1 and 2) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_ClienteComServicoAberto_CancelaEAnulaAsPropostasESolicitacoesPendentes()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 2, silentProviders: 1);
        int pending = scenario.ProposalIds[0];
        int declined = scenario.ProposalIds[1];
        using (var decline = await PostAsync(scenario.Client.Token, DeclineUrl(scenario.ServiceId, declined)))
        {
            Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        }

        using var response = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CANCELLED", service.GetProperty("status").GetString());
        Assert.Equal(scenario.Client.UserId, service.GetProperty("clientId").GetInt32());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("acceptedProposal").ValueKind);
        Assert.Equal(JsonValueKind.Null, service.GetProperty("actualCompletionDate").ValueKind);

        // The provider that did not answer gets VOID; the ones that submitted a proposal keep PROPOSAL_SUBMITTED.
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => r.GetProperty("professionalProfileId").GetInt32(), r => r.GetProperty("status").GetString());
        Assert.Equal("VOID", requests[scenario.Providers[2].ProfessionalProfileId]);
        Assert.Equal("PROPOSAL_SUBMITTED", requests[scenario.Providers[0].ProfessionalProfileId]);
        Assert.Equal("PROPOSAL_SUBMITTED", requests[scenario.Providers[1].ProfessionalProfileId]);

        var proposals = (await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId))
            .ToDictionary(p => p.GetProperty("proposalId").GetInt32());
        Assert.Equal("VOID", proposals[pending].GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, proposals[pending].GetProperty("decisionDate").ValueKind);
        Assert.Equal("DECLINED", proposals[declined].GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_ClienteComTrabalhoAberto_CancelaEAnulaAsPropostasPendentes()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);

        using var response = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CANCELLED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var proposals = await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal(2, proposals.Count);
        Assert.All(proposals, p => Assert.Equal("VOID", p.GetProperty("status").GetString()));
    }

    // ---------- E30: client, ASSIGNED (criterion 3) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_ClienteComServicoAtribuido_CancelaEMantemAPropostaAceite()
    {
        var scenario = await CreateAssignedScenarioAsync();

        using var response = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CANCELLED", service.GetProperty("status").GetString());
        var accepted = service.GetProperty("acceptedProposal");
        Assert.Equal(scenario.ProposalIds[0], accepted.GetProperty("proposalId").GetInt32());
        Assert.Equal("ACCEPTED", accepted.GetProperty("status").GetString());

        var proposals = (await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId))
            .ToDictionary(p => p.GetProperty("proposalId").GetInt32(), p => p.GetProperty("status").GetString());
        Assert.Equal("DECLINED", proposals[scenario.ProposalIds[1]]);
    }

    // ---------- E30: provider (criteria 4 and 5) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_PrestadorContratado_CancelaEDevolveAVistaDoPrestador()
    {
        var scenario = await CreateAssignedScenarioAsync();

        using var response = await PostAsync(scenario.Providers[0].Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CANCELLED", service.GetProperty("status").GetString());
        Assert.False(service.TryGetProperty("clientId", out _));
        Assert.Equal("ACCEPTED", service.GetProperty("acceptedProposal").GetProperty("status").GetString());
        Assert.Equal("CANCELLED", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    // Rule 2: a provider that only received a direct request.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_PrestadorQueSoRecebeuASolicitacao_Devolve403EMantemOsEstados()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 0, silentProviders: 1);
        string url = CancelUrl(scenario.ServiceId);

        using var response = await PostAsync(scenario.Providers[0].Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal("PENDING", service.GetProperty("directRequests")[0].GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_PrestadorComPropostaPendenteEmTrabalhoAberto_Devolve403()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);

        using var response = await PostAsync(scenario.Providers[0].Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("OPEN", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    // Criterion 5: a provider whose proposal was not accepted.
    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_PrestadorComPropostaNaoAceite_Devolve403EServicoFicaAtribuido()
    {
        var scenario = await CreateAssignedScenarioAsync();
        string url = CancelUrl(scenario.ServiceId);

        using var response = await PostAsync(scenario.Providers[1].Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
        Assert.Equal("ASSIGNED", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    // ---------- E30: other actors (criterion 6) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_OutroCliente_Devolve403()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        var other = await RegisterClientAsync(_client);
        string url = CancelUrl(scenario.ServiceId);

        using var response = await PostAsync(other.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
        Assert.Equal("OPEN", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_Administrador_Devolve403()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 0);
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await PostAsync(adminToken, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- E30: final states (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    public async Task Cancel_ServicoRealizado_Devolve409TransicaoInvalidaEMantemOEstado()
    {
        var scenario = await CreateAssignedScenarioAsync();
        using (var complete = await PostAsync(scenario.Client.Token, CompleteUrl(scenario.ServiceId)))
        {
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        }

        string url = CancelUrl(scenario.ServiceId);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await AssertProblemAsync(response, InvalidTransitionType, url);
        Assert.Equal("Não é possível cancelar um serviço no estado realizado.", problem.GetProperty("detail").GetString());
        Assert.Equal("COMPLETED", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    public async Task Cancel_ServicoJaCancelado_Devolve409TransicaoInvalida()
    {
        var scenario = await CreateAssignedScenarioAsync();
        using (var first = await PostAsync(scenario.Providers[0].Token, CancelUrl(scenario.ServiceId)))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        string url = CancelUrl(scenario.ServiceId);
        using var response = await PostAsync(scenario.Providers[0].Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
    }

    // A cancelled service no longer accepts decisions on the proposals (section 10).
    [Fact]
    [Trait("Requisito", "RF-F008-06")]
    public async Task Cancel_DepoisDeCancelado_AceitarUmaPropostaDevolve409()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        using (var cancel = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId)))
        {
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        }

        using var response = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ServiceId, scenario.ProposalIds[0]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_ServicoInexistente_Devolve404ParaOClienteEParaOPrestador()
    {
        var client = await RegisterClientAsync(_client);
        var provider = await CreateProviderWithProfileAsync(_client);
        string url = CancelUrl(int.MaxValue);

        using var asClient = await PostAsync(client.Token, url);
        using var asProvider = await PostAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.NotFound, asClient.StatusCode);
        await AssertProblemAsync(asClient, NotFoundType, url);
        Assert.Equal(HttpStatusCode.NotFound, asProvider.StatusCode);
    }

    // ---------- E31 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-06")]
    [Trait("Requisito", "RF-F008-05")]
    public async Task Complete_ServicoAtribuido_FicaRealizadoComADataDeRealizacao()
    {
        var scenario = await CreateAssignedScenarioAsync();

        using var response = await PostAsync(scenario.Client.Token, CompleteUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("COMPLETED", service.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, service.GetProperty("actualCompletionDate").ValueKind);
        Assert.False(service.GetProperty("reviewed").GetBoolean());
        Assert.Equal("ACCEPTED", service.GetProperty("acceptedProposal").GetProperty("status").GetString());

        // The date returned is the one stored (E22).
        var stored = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal(service.GetProperty("actualCompletionDate").GetDateTimeOffset(),
            stored.GetProperty("actualCompletionDate").GetDateTimeOffset());
    }

    // Section 10: OPEN → COMPLETED is not a transition.
    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    public async Task Complete_ServicoAberto_Devolve409TransicaoInvalida()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 1);
        string url = CompleteUrl(scenario.ServiceId);

        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
        Assert.Equal("OPEN", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-07")]
    public async Task Complete_ServicoCancelado_Devolve409TransicaoInvalida()
    {
        var scenario = await CreateAssignedScenarioAsync();
        using (var cancel = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId)))
        {
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        }

        string url = CompleteUrl(scenario.ServiceId);
        using var response = await PostAsync(scenario.Client.Token, url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
    }

    // Rule 1: the provider cannot mark the service as completed.
    [Fact]
    [Trait("Requisito", "RF-F007-06")]
    [Trait("Requisito", "RF-F008-05")]
    public async Task Complete_PrestadorContratado_Devolve403EServicoFicaAtribuido()
    {
        var scenario = await CreateAssignedScenarioAsync();
        string url = CompleteUrl(scenario.ServiceId);

        using var response = await PostAsync(scenario.Providers[0].Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
        Assert.Equal("ASSIGNED", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-06")]
    public async Task Complete_OutroCliente_Devolve403()
    {
        var scenario = await CreateAssignedScenarioAsync();
        var other = await RegisterClientAsync(_client);

        using var response = await PostAsync(other.Token, CompleteUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-06")]
    public async Task Complete_ServicoInexistente_Devolve404()
    {
        var client = await RegisterClientAsync(_client);
        string url = CompleteUrl(int.MaxValue);

        using var response = await PostAsync(client.Token, url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    // ---------- Helpers ----------

    // A service of a client and the providers in its category; the first "proposals" providers submitted a proposal.
    private sealed record Scenario(TestClient Client, int ServiceId, List<ProviderWithProfile> Providers, List<int> ProposalIds);

    private static string CancelUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/cancel";

    private static string CompleteUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/complete";

    private static string AcceptUrl(int serviceId, int proposalId) => $"{ServicesUrl}/{serviceId}/proposals/{proposalId}/accept";

    private static string DeclineUrl(int serviceId, int proposalId) => $"{ServicesUrl}/{serviceId}/proposals/{proposalId}/decline";

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

    private async Task<List<JsonElement>> ListProposalsAsync(string token, int serviceId)
    {
        using var response = await GetAsync(token, $"{ServicesUrl}/{serviceId}/proposals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    // A published job with two proposals: the first one accepted (ASSIGNED) and the second one DECLINED.
    private async Task<Scenario> CreateAssignedScenarioAsync()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);
        using var response = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ServiceId, scenario.ProposalIds[0]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return scenario;
    }

    // A service with direct requests to "proposals + silentProviders" providers; the first ones submit a proposal.
    private async Task<Scenario> CreateDirectScenarioAsync(int proposals, int silentProviders)
    {
        var providers = await CreateProvidersAsync(proposals + silentProviders);
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreateServiceAsync(
            client.Token,
            DirectRequestBody(providers[0].ServiceCategoryId, [.. providers.Select(p => p.ProfessionalProfileId)]));
        return new Scenario(client, serviceId, providers, await SubmitAllAsync(serviceId, providers.Take(proposals)));
    }

    // A published job; the first "proposals" providers of the category submit a proposal.
    private async Task<Scenario> CreatePublicScenarioAsync(int proposals)
    {
        var providers = await CreateProvidersAsync(Math.Max(proposals, 1));
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreateServiceAsync(client.Token, PublicBody(providers[0].ServiceCategoryId));
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

    private async Task<int> CreateServiceAsync(string clientToken, Dictionary<string, object?> body)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, clientToken, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }
}