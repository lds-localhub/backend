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

    // ---------- Criterion 1 ----------

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ContasSemServicos_DevolvemListaDeAlertasVazia()
    {
        var client = await RegisterClientAsync(_client);
        var provider = await CreateProviderAsync();

        var clientAccount = await FindAccountAsync(client.UserId, "CLIENT");
        var providerAccount = await FindAccountAsync(provider.UserId, "PROVIDER");

        Assert.Empty(clientAccount.GetProperty("alerts").EnumerateArray());
        Assert.Empty(providerAccount.GetProperty("alerts").EnumerateArray());
    }

    // ---------- Criteria 2 and 5 ----------

    // The provider cancels 4 of 5 assigned services: the cancellations count only for the provider.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PrestadorCancelaQuatroDeCinco_DevolveAlertaSoAoPrestador()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        for (int i = 0; i < 5; i++)
        {
            int serviceId = await CreateContractedAsync(client, provider);
            if (i < 4)
            {
                await PostOkAsync(provider.Token, $"{ServicesUrl}/{serviceId}/cancel");
            }
        }

        var providerAccount = await FindAccountAsync(provider.UserId, "PROVIDER");
        var clientAccount = await FindAccountAsync(client.UserId, "CLIENT");

        var alert = Assert.Single(providerAccount.GetProperty("alerts").EnumerateArray());
        Assert.Equal("ASSIGNED_CANCELLATIONS", alert.GetProperty("type").GetString());
        Assert.Equal(4, alert.GetProperty("count").GetInt32());
        Assert.Equal(5, alert.GetProperty("total").GetInt32());
        Assert.False(alert.TryGetProperty("averageRating", out _));
        Assert.False(alert.TryGetProperty("reviewCount", out _));
        Assert.Empty(clientAccount.GetProperty("alerts").EnumerateArray());
    }

    // ---------- Criterion 3 ----------

    // 2 of 2 cancelled by the provider (100%), but fewer than 3 assigned services.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PrestadorCancelaDoisDeDois_NaoDevolveAlerta()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        for (int i = 0; i < 2; i++)
        {
            int serviceId = await CreateContractedAsync(client, provider);
            await PostOkAsync(provider.Token, $"{ServicesUrl}/{serviceId}/cancel");
        }

        var account = await FindAccountAsync(provider.UserId, "PROVIDER");

        Assert.Empty(account.GetProperty("alerts").EnumerateArray());
    }

    // ---------- Criteria 4 and 5 ----------

    // The client cancels 2 of 4 assigned services (50%): the cancellations count only for the client.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ClienteCancelaDoisDeQuatro_DevolveAlertaSoAoCliente()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedWithClientCancellationsAsync(client, provider, assigned: 4, cancelled: 2);

        var clientAccount = await FindAccountAsync(client.UserId, "CLIENT");
        var providerAccount = await FindAccountAsync(provider.UserId, "PROVIDER");

        var alert = Assert.Single(clientAccount.GetProperty("alerts").EnumerateArray());
        Assert.Equal("ASSIGNED_CANCELLATIONS", alert.GetProperty("type").GetString());
        Assert.Equal(2, alert.GetProperty("count").GetInt32());
        Assert.Equal(4, alert.GetProperty("total").GetInt32());
        Assert.Empty(providerAccount.GetProperty("alerts").EnumerateArray());
    }

    // 1 of 4 (25%): below 50%.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ClienteCancelaUmDeQuatro_NaoDevolveAlerta()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedWithClientCancellationsAsync(client, provider, assigned: 4, cancelled: 1);

        var account = await FindAccountAsync(client.UserId, "CLIENT");

        Assert.Empty(account.GetProperty("alerts").EnumerateArray());
    }

    // ---------- Criterion 6 ----------

    // Ratings 2, 2 and 1: average 1.7 with 3 reviews.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PrestadorComAvaliacoes221_DevolveAlertaDeClassificacaoBaixa()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        foreach (int rating in new[] { 2, 2, 1 })
        {
            await CreateReviewedAsync(client, provider, rating);
        }

        var account = await FindAccountAsync(provider.UserId, "PROVIDER");

        var alert = Assert.Single(account.GetProperty("alerts").EnumerateArray());
        Assert.Equal("LOW_AVERAGE_RATING", alert.GetProperty("type").GetString());
        Assert.Equal(1.7m, alert.GetProperty("averageRating").GetDecimal());
        Assert.Equal(3, alert.GetProperty("reviewCount").GetInt32());
        Assert.False(alert.TryGetProperty("count", out _));
        Assert.False(alert.TryGetProperty("total", out _));
    }

    // Ratings 1 and 1: only 2 reviews. The provider without reviews is in criterion 1.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PrestadorComAvaliacoes11_NaoDevolveAlerta()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateReviewedAsync(client, provider, 1);
        await CreateReviewedAsync(client, provider, 1);

        var account = await FindAccountAsync(provider.UserId, "PROVIDER");

        Assert.Empty(account.GetProperty("alerts").EnumerateArray());
    }

    // ---------- Criterion 7 ----------

    // The alerts do not change the status: the account with an alert stays ACTIVE and keeps the filter ACTIVE. E06 and
    // E07 return the account without alerts. The filters and the order by name are in AccountsEndpointsTests.
    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_ContaComAlerta_MantemOEstadoEE06EE07DevolvemAContaSemAlertas()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedWithClientCancellationsAsync(client, provider, assigned: 3, cancelled: 3);

        var account = await FindAccountAsync(client.UserId, "CLIENT", "&status=ACTIVE");

        Assert.Equal("ACTIVE", account.GetProperty("status").GetString());
        Assert.Single(account.GetProperty("alerts").EnumerateArray());

        string adminToken = await LoginAsAdminAsync(_client);
        foreach (string operation in new[] { "deactivate", "activate" })
        {
            using var response = await SendWithTokenAsync(
                _client, HttpMethod.Post, $"{AccountsUrl}/{client.UserId}/{operation}", adminToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.TryGetProperty("alerts", out _));
        }
    }

    // Reads the pages of E05 with the role filter, because the test database keeps the accounts of the other tests.
    private async Task<JsonElement> FindAccountAsync(int userId, string role, string filters = "")
    {
        string adminToken = await LoginAsAdminAsync(_client);
        for (int page = 1; ; page++)
        {
            using var response = await SendWithTokenAsync(
                _client, HttpMethod.Get, $"{AccountsUrl}?role={role}{filters}&page={page}&pageSize=50", adminToken);
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

    // A PUBLIC service of the client with the proposal of the provider accepted (ASSIGNED).
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

        await PostOkAsync(client.Token, $"/api/proposals/{proposalId}/accept");
        return serviceId;
    }

    // The given number of assigned services, of which the first ones are cancelled by the client.
    private async Task CreateContractedWithClientCancellationsAsync(
        TestClient client, ProviderWithProfile provider, int assigned, int cancelled)
    {
        for (int i = 0; i < assigned; i++)
        {
            int serviceId = await CreateContractedAsync(client, provider);
            if (i < cancelled)
            {
                await PostOkAsync(client.Token, $"{ServicesUrl}/{serviceId}/cancel");
            }
        }
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