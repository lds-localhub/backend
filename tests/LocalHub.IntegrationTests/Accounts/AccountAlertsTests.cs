using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Accounts;

// F002: conduct alerts of the accounts in E05 (RF-F002-01, rules 5 to 7). Each test uses new clients and providers, so
// the alerts only depend on its data.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AccountAlertsTests(LocalHubApiFactory factory)
{
    private const string AccountsUrl = "/api/accounts";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ContaSemServicos_DevolveListaDeAlertasVazia()
    {
        var client = await RegisterClientAsync(_client);

        var account = await FindAccountAsync(client.UserId, "CLIENT");

        Assert.Empty(account.GetProperty("alerts").EnumerateArray());
    }

    // 2 of 3 assigned services cancelled by the client (67%): the client has the alert; the provider did not cancel.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ClienteCancelaDoisDeTresAtribuidos_DevolveAlertaDeCancelamentos()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int first = await CreateContractedAsync(client, provider);
        int second = await CreateContractedAsync(client, provider);
        await CreateContractedAsync(client, provider);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{first}/cancel");
        await PostOkAsync(client.Token, $"{ServicesUrl}/{second}/cancel");

        var clientAccount = await FindAccountAsync(client.UserId, "CLIENT");
        var providerAccount = await FindAccountAsync(provider.UserId, "PROVIDER");

        var alert = Assert.Single(clientAccount.GetProperty("alerts").EnumerateArray());
        Assert.Equal("ASSIGNED_CANCELLATIONS", alert.GetProperty("type").GetString());
        Assert.Equal(2, alert.GetProperty("count").GetInt32());
        Assert.Equal(3, alert.GetProperty("total").GetInt32());
        Assert.False(alert.TryGetProperty("averageRating", out _));
        Assert.Empty(providerAccount.GetProperty("alerts").EnumerateArray());
    }

    // Volume below the minimum: 2 of 2 cancelled (100%), but fewer than 3 assigned services.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ClienteCancelaDoisDeDoisAtribuidos_NaoDevolveAlerta()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int first = await CreateContractedAsync(client, provider);
        int second = await CreateContractedAsync(client, provider);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{first}/cancel");
        await PostOkAsync(client.Token, $"{ServicesUrl}/{second}/cancel");

        var account = await FindAccountAsync(client.UserId, "CLIENT");

        Assert.Empty(account.GetProperty("alerts").EnumerateArray());
    }

    // 3 reviews of 1 star: average 1.0 with 3 reviews.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PrestadorComMediaUmEmTresAvaliacoes_DevolveAlertaDeClassificacaoBaixa()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        for (int i = 0; i < 3; i++)
        {
            await CreateReviewedAsync(client, provider, 1);
        }

        var account = await FindAccountAsync(provider.UserId, "PROVIDER");

        var alert = Assert.Single(account.GetProperty("alerts").EnumerateArray());
        Assert.Equal("LOW_AVERAGE_RATING", alert.GetProperty("type").GetString());
        Assert.Equal(1.0m, alert.GetProperty("averageRating").GetDecimal());
        Assert.Equal(3, alert.GetProperty("reviewCount").GetInt32());
        Assert.False(alert.TryGetProperty("count", out _));
    }

    // Average 1.0 but only 2 reviews.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PrestadorComMediaUmEmDuasAvaliacoes_NaoDevolveAlerta()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateReviewedAsync(client, provider, 1);
        await CreateReviewedAsync(client, provider, 1);

        var account = await FindAccountAsync(provider.UserId, "PROVIDER");

        Assert.Empty(account.GetProperty("alerts").EnumerateArray());
    }

    // Reads the pages of E05 with the role filter, because the test database keeps the accounts of the other tests.
    private async Task<JsonElement> FindAccountAsync(int userId, string role)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        for (int page = 1; ; page++)
        {
            using var response = await SendWithTokenAsync(
                _client, HttpMethod.Get, $"{AccountsUrl}?role={role}&page={page}&pageSize=50", adminToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.NotEmpty(items);
            foreach (var item in items.Where(i => i.GetProperty("userId").GetInt32() == userId))
            {
                return item.Clone();
            }
        }
    }

    private async Task PostOkAsync(string token, string url)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<ProviderWithProfile> CreateProviderAsync()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken);
        var (token, userId, email) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(categoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryId);
    }

    // A published job of the client with the proposal of the provider accepted (ASSIGNED).
    private async Task<int> CreateContractedAsync(TestClient client, ProviderWithProfile provider)
    {
        using var created = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(provider.ServiceCategoryId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        int serviceId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();

        using var submitted = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
            provider.Token, new { amount = 80.00m, conditions = "Material incluído." });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        int proposalId = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();

        await PostOkAsync(client.Token, $"{ServicesUrl}/{serviceId}/proposals/{proposalId}/accept");
        return serviceId;
    }

    // A contracted service, completed and reviewed by the client.
    private async Task CreateReviewedAsync(TestClient client, ProviderWithProfile provider, int rating)
    {
        int serviceId = await CreateContractedAsync(client, provider);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{serviceId}/complete");
        using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/review",
            client.Token, new { rating });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}