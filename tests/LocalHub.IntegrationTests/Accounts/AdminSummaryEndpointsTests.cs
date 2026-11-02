using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Domain.Accounts;
using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;

namespace LocalHub.IntegrationTests.Accounts;

// F002 and F003: E39 (summary of the administration), reserved to the administrator.
// The 401 and 403 cases of this endpoint are in AccessControlTests. The test database keeps the data of the other
// tests, so the counters are compared with the database and with the values read before each change.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AdminSummaryEndpointsTests(LocalHubApiFactory factory)
{
    private const string SummaryUrl = "/api/admin/summary";
    private static readonly string[] SummaryFields = ["activeAccounts", "inactiveAccounts", "activeCategories"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    [Trait("Requisito", "RF-F003-04")]
    public async Task Summary_Administrador_Devolve200ComOsContadoresSemContasDeAdministrador()
    {
        string adminToken = await LoginAsAdminAsync(_client);

        var summary = await GetSummaryAsync(adminToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        var accounts = db.Users.Where(u => u.Role != UserRole.Admin);
        Assert.Equal(await accounts.CountAsync(u => u.Status == ActivationStatus.Active), summary.ActiveAccounts);
        Assert.Equal(await accounts.CountAsync(u => u.Status == ActivationStatus.Inactive), summary.InactiveAccounts);
        Assert.Equal(
            await db.ServiceCategories.CountAsync(c => c.Status == ActivationStatus.Active), summary.ActiveCategories);

        // The administrator created by the DataSeeder is active and is not counted.
        int activeUsers = await db.Users.CountAsync(u => u.Status == ActivationStatus.Active);
        Assert.True(activeUsers > summary.ActiveAccounts);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    [Trait("Requisito", "RF-F002-02")]
    [Trait("Requisito", "RF-F003-03")]
    public async Task Summary_DepoisDeRegistarDesativarECriarCategorias_AtualizaOsContadores()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var before = await GetSummaryAsync(adminToken);

        // Two new accounts (one client and one provider), one of them deactivated; two new categories, one deactivated.
        await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");
        string providerEmail = NewEmail();
        await RegisterAndLoginAsync(_client, providerEmail, "PROVIDER");
        await DeactivateAsync(adminToken, $"/api/accounts/{await GetUserIdAsync(providerEmail)}/deactivate");
        await CreateCategoryAsync(_client, adminToken);
        int inactiveCategoryId = await CreateCategoryAsync(_client, adminToken);
        await DeactivateAsync(adminToken, $"/api/categories/{inactiveCategoryId}/deactivate");

        var after = await GetSummaryAsync(adminToken);

        Assert.Equal(before.ActiveAccounts + 1, after.ActiveAccounts);
        Assert.Equal(before.InactiveAccounts + 1, after.InactiveAccounts);
        Assert.Equal(before.ActiveCategories + 1, after.ActiveCategories);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task Summary_Administrador_DevolveApenasOsTresContadores()
    {
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, SummaryUrl, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(SummaryFields, body.EnumerateObject().Select(p => p.Name).ToArray());
    }

    private async Task<(int ActiveAccounts, int InactiveAccounts, int ActiveCategories)> GetSummaryAsync(string adminToken)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, SummaryUrl, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            body.GetProperty("activeAccounts").GetInt32(),
            body.GetProperty("inactiveAccounts").GetInt32(),
            body.GetProperty("activeCategories").GetInt32());
    }

    private async Task DeactivateAsync(string adminToken, string url)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<int> GetUserIdAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.UserId).SingleAsync();
    }
}