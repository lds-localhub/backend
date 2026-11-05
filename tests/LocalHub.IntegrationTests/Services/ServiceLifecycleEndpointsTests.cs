using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Services;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ServiceLifecycleEndpointsTests(LocalHubApiFactory factory)
{
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string InvalidTransitionType = "https://localhub.local/erros/transicao-invalida";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_ClientWithOpenService_CancelsAndVoidsPendingProposalsAndDirectRequests()
    {
        var scenario = await CreateDirectScenarioAsync(proposals: 2, silentProviders: 1);
        int pending = scenario.ProposalIds[0];
        int declined = scenario.ProposalIds[1];
        using (var decline = await PostAsync(scenario.Client.Token, DeclineUrl(declined)))
        {
            Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        }

        using var response = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["serviceId", "status"], body.EnumerateObject().Select(f => f.Name));
        Assert.Equal(scenario.ServiceId, body.GetProperty("serviceId").GetInt32());
        Assert.Equal("CANCELLED", body.GetProperty("status").GetString());

        var service = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal(JsonValueKind.Null, service.GetProperty("actualCompletionDate").ValueKind);

        // The provider that did not answer gets VOID; the ones that submitted a proposal keep PROPOSAL_SUBMITTED.
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => r.GetProperty("professionalProfile").GetProperty("professionalProfileId").GetInt32(),
            r => r.GetProperty("status").GetString());
        Assert.Equal("VOID", requests[scenario.Providers[2].ProfessionalProfileId]);
        Assert.Equal("PROPOSAL_SUBMITTED", requests[scenario.Providers[0].ProfessionalProfileId]);
        Assert.Equal("PROPOSAL_SUBMITTED", requests[scenario.Providers[1].ProfessionalProfileId]);

        var proposals = (await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId))
            .ToDictionary(p => p.GetProperty("proposalId").GetInt32());
        Assert.Equal("VOID", proposals[pending].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, proposals[pending].GetProperty("decisionDate").ValueKind);
        Assert.Equal("DECLINED", proposals[declined].GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_ClientWithOpenPublicService_CancelsAndVoidsPendingProposals()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);

        using var response = await PostAsync(scenario.Client.Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CANCELLED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var proposals = await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal(2, proposals.Count);
        Assert.All(proposals, p => Assert.Equal("VOID", p.GetProperty("status").GetString()));
    }

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    [Trait("Requisito", "RF-F008-04")]
    public async Task Cancel_ContractedProvider_CancelsAndReturnsServiceAndState()
    {
        var scenario = await CreateAssignedScenarioAsync();

        using var response = await PostAsync(scenario.Providers[0].Token, CancelUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["serviceId", "status"], body.EnumerateObject().Select(f => f.Name));
        Assert.Equal("CANCELLED", body.GetProperty("status").GetString());
        var accepted = Assert.Single(await ListProposalsAsync(scenario.Client.Token, scenario.ServiceId),
            p => p.GetProperty("proposalId").GetInt32() == scenario.ProposalIds[0]);
        Assert.Equal("ACCEPTED", accepted.GetProperty("status").GetString());
        Assert.Equal("CANCELLED", (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_ProviderWithOnlyDirectRequest_Returns403AndKeepsStates()
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
    [Trait("Requisito", "RF-F007-05")]
    public async Task Cancel_OtherClient_Returns403()
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
    [Trait("Requisito", "RF-F007-07")]
    [Trait("Requisito", "RF-F008-06")]
    public async Task Cancel_CompletedService_Returns409InvalidTransitionAndKeepsState()
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
    [Trait("Requisito", "RF-F007-06")]
    [Trait("Requisito", "RF-F008-05")]
    public async Task Complete_AssignedService_BecomesCompletedWithCompletionDate()
    {
        var scenario = await CreateAssignedScenarioAsync();

        using var response = await PostAsync(scenario.Client.Token, CompleteUrl(scenario.ServiceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["serviceId", "status", "actualCompletionDate"], body.EnumerateObject().Select(f => f.Name));
        Assert.Equal("COMPLETED", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("actualCompletionDate").ValueKind);

        var stored = await GetServiceAsync(scenario.Client.Token, scenario.ServiceId);
        Assert.Equal(body.GetProperty("actualCompletionDate").GetDateTimeOffset(),
            stored.GetProperty("actualCompletionDate").GetDateTimeOffset());
        Assert.False(stored.GetProperty("reviewed").GetBoolean());
        var accepted = Assert.Single(stored.GetProperty("proposals").EnumerateArray(),
            p => p.GetProperty("status").GetString() == "ACCEPTED");
        Assert.Equal(scenario.ProposalIds[0], accepted.GetProperty("proposalId").GetInt32());
    }

    // The first "proposals" providers submitted a proposal.
    private sealed record Scenario(TestClient Client, int ServiceId, List<ProviderWithProfile> Providers, List<int> ProposalIds);

    private static string CancelUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/cancel";

    private static string CompleteUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/complete";

    private static string AcceptUrl(int proposalId) => $"/api/proposals/{proposalId}/accept";

    private static string DeclineUrl(int proposalId) => $"/api/proposals/{proposalId}/decline";

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
        var service = await GetServiceAsync(token, serviceId);
        var proposals = service.GetProperty("proposals").EnumerateArray().ToList();
        proposals.AddRange(service.GetProperty("directRequests").EnumerateArray()
            .Select(r => r.GetProperty("proposal"))
            .Where(p => p.ValueKind != JsonValueKind.Null));
        return proposals;
    }

    // A PUBLIC service with two proposals: the first one accepted (ASSIGNED) and the second one DECLINED.
    private async Task<Scenario> CreateAssignedScenarioAsync()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);
        using var response = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ProposalIds[0]));
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

    // A PUBLIC service; the first "proposals" providers of the category submit a proposal.
    private async Task<Scenario> CreatePublicScenarioAsync(int proposals)
    {
        var providers = await CreateProvidersAsync(Math.Max(proposals, 1));
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreateServiceAsync(client.Token, PublicBody(providers[0].ServiceCategoryId));
        return new Scenario(client, serviceId, providers, await SubmitAllAsync(serviceId, providers.Take(proposals)));
    }

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