using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Accounts;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AccountsEndpointsTests(LocalHubApiFactory factory)
{
    private const string AccountsUrl = "/api/accounts";
    private const string MeUrl = "/api/auth/me";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_NoFilters_ReturnsOnlyClientsAndProvidersSortedByName()
    {
        string tag = Guid.NewGuid().ToString("N");
        // Registered in reverse order, so the order of the list comes from the name.
        var provider = await RegisterAccountAsync("PROVIDER", $"Teste Lista B {tag}");
        var client = await RegisterAccountAsync("CLIENT", $"Teste Lista A {tag}");
        string adminToken = await LoginAsAdminAsync(_client);

        var accounts = await ListAllAsync(adminToken, "");

        Assert.All(accounts, a =>
        {
            string? role = a.GetProperty("role").GetString();
            Assert.True(role == "CLIENT" || role == "PROVIDER");
        });
        var emails = accounts.Select(a => a.GetProperty("email").GetString()).ToList();
        Assert.DoesNotContain(AdminEmail, emails);
        Assert.Contains(client.Email, emails);
        Assert.Contains(provider.Email, emails);
        Assert.True(emails.IndexOf(client.Email) < emails.IndexOf(provider.Email));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public async Task Deactivate_ActiveAccount_Returns200WithInactiveAccountAndRevokesSessions()
    {
        var account = await RegisterAccountAsync("CLIENT");
        await LoginAndGetTokenAsync(_client, account.Email, ValidPassword);
        await LoginAndGetTokenAsync(_client, account.Email, ValidPassword);
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{account.UserId}/deactivate", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(account.UserId, body.GetProperty("userId").GetInt32());
        Assert.Equal(account.Email, body.GetProperty("email").GetString());
        Assert.Equal("CLIENT", body.GetProperty("role").GetString());
        Assert.Equal("INACTIVE", body.GetProperty("status").GetString());

        var sessions = await GetSessionsAsync(account.UserId);
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, revokedAt => Assert.NotNull(revokedAt));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-04")]
    public async Task Login_AccountDeactivatedByAdministrator_Returns403AccountDeactivatedWithoutSession()
    {
        var account = await RegisterAccountAsync("PROVIDER");
        string adminToken = await LoginAsAdminAsync(_client);
        await DeactivateAsync(adminToken, account.UserId);

        using var response = await LoginAsync(_client, account.Email, ValidPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await AssertProblemAsync(response, "https://localhub.local/erros/conta-desativada", "/api/auth/login");
        Assert.Equal(
            "A sua conta foi desativada. Para saber o motivo, contacte suporte@localhub.test.",
            body.GetProperty("detail").GetString());
        Assert.Empty(await GetSessionsAsync(account.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-03")]
    public async Task Activate_InactiveAccount_Returns200ShownActiveInListAndUserCanLogInAgain()
    {
        var account = await RegisterAccountAsync("CLIENT");
        string adminToken = await LoginAsAdminAsync(_client);
        await DeactivateAsync(adminToken, account.UserId);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{account.UserId}/activate", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
        var listed = (await ListAllAsync(adminToken, "role=CLIENT"))
            .Single(a => a.GetProperty("userId").GetInt32() == account.UserId);
        Assert.Equal("ACTIVE", listed.GetProperty("status").GetString());
        string userToken = await LoginAndGetTokenAsync(_client, account.Email, ValidPassword);
        using var me = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, userToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    public async Task ResetPassword_TemporaryPassword_LogsInAndPreviousPasswordStopsWorking()
    {
        var account = await RegisterAccountAsync("CLIENT");
        string adminToken = await LoginAsAdminAsync(_client);

        string temporaryPassword = await ResetPasswordAsync(adminToken, account.UserId);

        using var newLogin = await LoginAsync(_client, account.Email, temporaryPassword);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var oldLogin = await LoginAsync(_client, account.Email, ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    public async Task ResetPassword_PreviousSessions_Return401()
    {
        var account = await RegisterAccountAsync("PROVIDER");
        string userToken = await LoginAndGetTokenAsync(_client, account.Email, ValidPassword);
        await LoginAndGetTokenAsync(_client, account.Email, ValidPassword);
        string adminToken = await LoginAsAdminAsync(_client);

        await ResetPasswordAsync(adminToken, account.UserId);

        using var me = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, userToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        var sessions = await GetSessionsAsync(account.UserId);
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, revokedAt => Assert.NotNull(revokedAt));
    }

    private async Task<string> ResetPasswordAsync(string adminToken, int userId)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{userId}/reset-password", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("temporaryPassword").GetString()!;
    }

    private async Task<string> GetPasswordHashAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.Where(u => u.UserId == userId).Select(u => u.PasswordHash).SingleAsync();
    }

    private async Task<(int UserId, string Email)> RegisterAccountAsync(string role, string name = "Teste Contas")
    {
        string email = NewEmail();
        using var response = await RegisterAsync(_client, name, email, ValidPassword, role);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("userId").GetInt32(), email);
    }

    private async Task DeactivateAsync(string adminToken, int userId)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{userId}/deactivate", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The test database keeps the accounts of the other tests, so every page is read.
    private async Task<List<JsonElement>> ListAllAsync(string adminToken, string filters)
    {
        var accounts = new List<JsonElement>();
        for (int page = 1; ; page++)
        {
            string url = $"{AccountsUrl}?{filters}&page={page}&pageSize=50";
            using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, adminToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = body.GetProperty("items").EnumerateArray().Select(i => i.Clone()).ToList();
            accounts.AddRange(items);
            if (items.Count == 0 || accounts.Count >= body.GetProperty("total").GetInt32())
            {
                return accounts;
            }
        }
    }

    private async Task<List<DateTimeOffset?>> GetSessionsAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.UserSessions.Where(s => s.UserId == userId).Select(s => s.RevokedAt).ToListAsync();
    }
}