using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Services;

// RF-F007-09 and RF-F008-01: the history of states of the service (ServiceStatusHistory) in the field history of E22
// and E36, filled by E28, E30 and E31.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ServiceStatusHistoryEndpointsTests(LocalHubApiFactory factory)
{

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E36: provider ----------

    // API documentation, E36: only the role of the author, so the hired provider is not identified (RNF-06).
    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task GetDirectRequest_StatusHistory_ShowsOnlyAuthorRole()
    {
        var scenario = await CreateDirectScenarioAsync();
        var provider = scenario.Providers[0];
        using (var accept = await PostAsync(
                   scenario.Client.Token, AcceptUrl(scenario.ProposalIds[0])))
        {
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        }

        using (var cancel = await PostAsync(provider.Token, CancelUrl(scenario.ServiceId)))
        {
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        }

        int directRequestId = (await GetServiceAsync(scenario.Client.Token, scenario.ServiceId))
            .GetProperty("directRequests").EnumerateArray().Single().GetProperty("directRequestId").GetInt32();
        using var response = await GetAsync(provider.Token, $"/api/direct-requests/{directRequestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var history = body.GetProperty("service").GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(2, history.Count);
        AssertAnonymousChange(history[0], "OPEN", "ASSIGNED", "CLIENT");
        AssertAnonymousChange(history[1], "ASSIGNED", "CANCELLED", "PROVIDER");
    }

    // ---------- Helpers ----------

    // A service of a client and the providers in its category; the first "proposals" providers submitted a proposal.
    private sealed record Scenario(TestClient Client, int ServiceId, List<ProviderWithProfile> Providers, List<int> ProposalIds);

    private static string CancelUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/cancel";

    private static string CompleteUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/complete";

    private static string AcceptUrl(int proposalId) => $"/api/proposals/{proposalId}/accept";

    private static string DeclineUrl(int proposalId) => $"/api/proposals/{proposalId}/decline";

    // changedAt is in UTC (API documentation, conventions).
    private static DateTimeOffset ChangedAt(JsonElement change)
    {
        string value = change.GetProperty("changedAt").GetString()!;
        Assert.EndsWith("Z", value);
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private static void AssertAnonymousChange(JsonElement change, string previousStatus, string newStatus, string role)
    {
        Assert.Equal(previousStatus, change.GetProperty("previousStatus").GetString());
        Assert.Equal(newStatus, change.GetProperty("newStatus").GetString());
        Assert.Equal(role, change.GetProperty("changedByRole").GetString());
        Assert.False(change.TryGetProperty("changedByUserId", out _));
        Assert.False(change.TryGetProperty("changedByName", out _));
        ChangedAt(change);
    }

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

    // A PUBLIC service with two proposals: the first one accepted (ASSIGNED) and the second one DECLINED.
    private async Task<Scenario> CreateAssignedScenarioAsync()
    {
        var scenario = await CreatePublicScenarioAsync(proposals: 2);
        using var response = await PostAsync(scenario.Client.Token, AcceptUrl(scenario.ProposalIds[0]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return scenario;
    }

    // A service with a direct request to one provider, who submitted a proposal.
    private async Task<Scenario> CreateDirectScenarioAsync()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreateServiceAsync(
            client.Token, DirectRequestBody(provider.ServiceCategoryId, provider.ProfessionalProfileId));
        return new Scenario(client, serviceId, [provider], await SubmitAllAsync(serviceId, [provider]));
    }

    // A PUBLIC service; the first "proposals" providers of the category submit a proposal.
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