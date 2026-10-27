using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Accounts;

// F002: E05 (list accounts), E06 (deactivate account), E07 (activate account) and E41 (reset password), reserved to
// the administrator.
// The 401 and 403 cases of these endpoints are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AccountsEndpointsTests(LocalHubApiFactory factory)
{
    private const string AccountsUrl = "/api/accounts";
    private const string MeUrl = "/api/users/me";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_SemFiltros_DevolveApenasClientesEPrestadoresOrdenadosPorNome()
    {
        string tag = Guid.NewGuid().ToString("N");
        // Registered in reverse order, so that the order of the list comes from the name.
        var provider = await RegisterAccountAsync("PROVIDER", $"Teste Lista B {tag}");
        var client = await RegisterAccountAsync("CLIENT", $"Teste Lista A {tag}");
        string adminToken = await LoginAsAdminAsync(_client);

        var accounts = await ListAllAsync(adminToken, "");

        Assert.All(accounts, a => Assert.True(a.GetProperty("role").GetString() is "CLIENT" or "PROVIDER"));
        var emails = accounts.Select(a => a.GetProperty("email").GetString()).ToList();
        Assert.DoesNotContain(AdminEmail, emails);
        Assert.Contains(client.Email, emails);
        Assert.Contains(provider.Email, emails);
        Assert.True(emails.IndexOf(client.Email) < emails.IndexOf(provider.Email));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_FiltroPrestadorDesativada_DevolveApenasPrestadoresDesativados()
    {
        var inactiveProvider = await RegisterAccountAsync("PROVIDER");
        var inactiveClient = await RegisterAccountAsync("CLIENT");
        var activeProvider = await RegisterAccountAsync("PROVIDER");
        string adminToken = await LoginAsAdminAsync(_client);
        await DeactivateAsync(adminToken, inactiveProvider.UserId);
        await DeactivateAsync(adminToken, inactiveClient.UserId);

        var accounts = await ListAllAsync(adminToken, "role=PROVIDER&status=INACTIVE");

        Assert.All(accounts, a =>
        {
            Assert.Equal("PROVIDER", a.GetProperty("role").GetString());
            Assert.Equal("INACTIVE", a.GetProperty("status").GetString());
        });
        var emails = accounts.Select(a => a.GetProperty("email").GetString()).ToList();
        Assert.Contains(inactiveProvider.Email, emails);
        Assert.DoesNotContain(inactiveClient.Email, emails);
        Assert.DoesNotContain(activeProvider.Email, emails);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_Pagina2ComTamanho1_DevolveUmaContaComTotalEPagina()
    {
        await RegisterAccountAsync("CLIENT");
        await RegisterAccountAsync("CLIENT");
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, $"{AccountsUrl}?page=2&pageSize=1", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("total").GetInt32() >= 2);
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_PaginaMuitoGrande_Devolve200SemContas()
    {
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, $"{AccountsUrl}?page={int.MaxValue}&pageSize=50", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(int.MaxValue, body.GetProperty("page").GetInt32());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("role=ADMIN", "role")]
    [InlineData("role=XYZ", "role")]
    [InlineData("status=BLOQUEADA", "status")]
    [InlineData("page=0", "page")]
    [InlineData("page=abc", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    [Trait("Requisito", "RF-F002-01")]
    public async Task List_FiltroOuPaginacaoInvalidos_Devolve400ComErroNoCampo(string query, string field)
    {
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, $"{AccountsUrl}?{query}", adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, AccountsUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public async Task Deactivate_ContaAtiva_Devolve200ComContaDesativadaERevogaAsSessoes()
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
    [Trait("Requisito", "RF-F002-02")]
    [Trait("Requisito", "RNF-02")]
    public async Task Deactivate_SessaoIniciadaAntesDaDesativacao_PedidoSeguinteDevolve401()
    {
        var account = await RegisterAccountAsync("PROVIDER");
        string userToken = await LoginAndGetTokenAsync(_client, account.Email, ValidPassword);
        string adminToken = await LoginAsAdminAsync(_client);

        await DeactivateAsync(adminToken, account.UserId);
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, userToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public async Task Deactivate_ContaDeAdministrador_Devolve409EMantemContaAtiva()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int adminId = await GetMyUserIdAsync(adminToken);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{adminId}/deactivate", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ConflictType, $"{AccountsUrl}/{adminId}/deactivate");
        using var me = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, adminToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public async Task Deactivate_ContaJaDesativada_Devolve409()
    {
        var account = await RegisterAccountAsync("CLIENT");
        string adminToken = await LoginAsAdminAsync(_client);
        await DeactivateAsync(adminToken, account.UserId);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{account.UserId}/deactivate", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ConflictType, $"{AccountsUrl}/{account.UserId}/deactivate");
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("activate")]
    [Trait("Requisito", "RF-F002-02")]
    [Trait("Requisito", "RF-F002-03")]
    public async Task DeactivateOuActivate_ContaInexistente_Devolve404(string operation)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string url = $"{AccountsUrl}/{int.MaxValue}/{operation}";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-04")]
    public async Task Login_ContaDesativadaPeloAdministrador_Devolve403ContaDesativadaSemIniciarSessao()
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
    public async Task Activate_ContaDesativada_Devolve200SurgeAtivaNaListaEUtilizadorVoltaAIniciarSessao()
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
    [Trait("Requisito", "RF-F002-03")]
    public async Task Activate_ContaAtiva_Devolve409()
    {
        var account = await RegisterAccountAsync("PROVIDER");
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{AccountsUrl}/{account.UserId}/activate", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ConflictType, $"{AccountsUrl}/{account.UserId}/activate");
    }

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    [Trait("Requisito", "RNF-01")]
    public async Task ResetPassword_ContaDeCliente_Devolve200ComPalavraPasseTemporariaValida()
    {
        var account = await RegisterAccountAsync("CLIENT");
        string adminToken = await LoginAsAdminAsync(_client);

        string temporaryPassword = await ResetPasswordAsync(adminToken, account.UserId);

        Assert.True(temporaryPassword.Length >= 8);
        Assert.Contains(temporaryPassword, char.IsLetter);
        Assert.Contains(temporaryPassword, char.IsDigit);
        Assert.NotEqual(temporaryPassword, await GetPasswordHashAsync(account.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    public async Task ResetPassword_PalavraPasseTemporaria_IniciaSessaoEAnteriorDeixaDeFuncionar()
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
    public async Task ResetPassword_SessoesAnteriores_PassamADevolver401()
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

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    public async Task ResetPassword_ContaDeAdministrador_Devolve409()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int adminId = await GetMyUserIdAsync(adminToken);
        string url = $"{AccountsUrl}/{adminId}/reset-password";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ConflictType, url);
        using var me = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, adminToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    public async Task ResetPassword_ContaDesativada_MantemEstadoDaConta()
    {
        var account = await RegisterAccountAsync("PROVIDER");
        string adminToken = await LoginAsAdminAsync(_client);
        await DeactivateAsync(adminToken, account.UserId);

        string temporaryPassword = await ResetPasswordAsync(adminToken, account.UserId);

        var listed = (await ListAllAsync(adminToken, "role=PROVIDER"))
            .Single(a => a.GetProperty("userId").GetInt32() == account.UserId);
        Assert.Equal("INACTIVE", listed.GetProperty("status").GetString());
        using var login = await LoginAsync(_client, account.Email, temporaryPassword);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        await AssertProblemAsync(login, "https://localhub.local/erros/conta-desativada", "/api/auth/login");
    }

    [Fact]
    [Trait("Requisito", "RF-F002-05")]
    public async Task ResetPassword_ContaInexistente_Devolve404()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string url = $"{AccountsUrl}/{int.MaxValue}/reset-password";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
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

    private async Task<int> GetMyUserIdAsync(string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, token);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("userId").GetInt32();
    }

    // Reads every page of E05, because the test database keeps the accounts created by the other tests.
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