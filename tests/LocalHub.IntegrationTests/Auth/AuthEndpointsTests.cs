using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AuthEndpointsTests(LocalHubApiFactory factory)
{
    private const string PasswordUrl = "/api/users/me/password";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_ValidClient_Returns201WithoutLocationAndActiveAccount()
    {
        string email = NewEmail();

        using var response = await RegisterAsync(_client, "Ana Costa", email, ValidPassword, "CLIENT");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("CLIENT", body.GetProperty("role").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_InvalidFieldsAndAdministratorRole_Returns400WithErrorPerField()
    {
        using var response = await RegisterAsync(_client, "", "invalido", "curta", "ADMIN");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(
            response, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "/api/auth/register");
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("role", out _));
    }

    [Theory]
    [InlineData("abcdefgh")] // 8 characters, no digit
    [InlineData("1234567")] // no letter and only 7 characters
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_WeakPassword_Returns400WithPasswordErrorAndCreatesNoAccount(string password)
    {
        string email = NewEmail();

        using var response = await RegisterAsync(_client, "Ana Costa", email, password, "CLIENT");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.Equal(0, await CountUsersAsync(email));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-02")]
    [Trait("Requisito", "RF-F001-05")]
    public async Task Login_SeededAdministrator_Returns200WithTokenAndEightHourSession()
    {
        using var response = await LoginAsync(_client, AdminEmail, LocalHubApiFactory.SeedPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("ADMIN", body.GetProperty("user").GetProperty("role").GetString());

        // The expiry is not in the response, so it is read from the session.
        var expiresAt = await GetLastSessionExpiryAsync(AdminEmail);
        Assert.InRange(expiresAt - DateTimeOffset.UtcNow, TimeSpan.FromHours(7.9), TimeSpan.FromHours(8));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-02")]
    public async Task Login_WrongPasswordOrUnknownEmail_Returns401WithSameMessage()
    {
        using var wrongPassword = await LoginAsync(_client, AdminEmail, "errada123");
        using var unknownEmail = await LoginAsync(_client, NewEmail(), "errada123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var first = await AssertProblemAsync(
            wrongPassword, "https://localhub.local/erros/credenciais-invalidas", "/api/auth/login");
        var second = await AssertProblemAsync(
            unknownEmail, "https://localhub.local/erros/credenciais-invalidas", "/api/auth/login");
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F001-06")]
    public async Task Login_AdministratorOnMobileApp_Returns403WebOnlyAdministrationWithoutSession()
    {
        int sessionsBefore = await CountSessionsAsync(AdminEmail);

        using var response = await LoginAsync(_client, AdminEmail, LocalHubApiFactory.SeedPassword, "MOBILE");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await AssertProblemAsync(response, "https://localhub.local/erros/administracao-so-web", "/api/auth/login");
        Assert.Equal("A administração está disponível apenas na aplicação web.", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("token", out _));
        Assert.Equal(sessionsBefore, await CountSessionsAsync(AdminEmail));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-07")]
    [Trait("Requisito", "RNF-01")]
    public async Task ChangePassword_ValidData_NewPasswordReplacesPrevious()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email, "CLIENT");
        string oldHash = await GetPasswordHashAsync(email);

        using var response = await ChangePasswordAsync(token, ValidPassword, "jardim2027");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var newLogin = await LoginAsync(_client, email, "jardim2027");
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var oldLogin = await LoginAsync(_client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        string newHash = await GetPasswordHashAsync(email);
        Assert.NotEqual(oldHash, newHash);
        Assert.NotEqual("jardim2027", newHash);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-07")]
    public async Task ChangePassword_ValidData_RevokesOtherSessionsAndKeepsCurrent()
    {
        string email = NewEmail();
        string tokenA = await RegisterAndLoginAsync(_client, email, "CLIENT");
        string tokenB = await LoginAndGetTokenAsync(_client, email, ValidPassword);

        using var response = await ChangePasswordAsync(tokenA, ValidPassword, "jardim2027");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var meB = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/auth/me", tokenB);
        Assert.Equal(HttpStatusCode.Unauthorized, meB.StatusCode);
        using var meA = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/auth/me", tokenA);
        Assert.Equal(HttpStatusCode.OK, meA.StatusCode);
    }

    // SendWithTokenAsync only sends requests without a body.
    private async Task<HttpResponseMessage> ChangePasswordAsync(string token, string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, PasswordUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { currentPassword, newPassword });
        return await _client.SendAsync(request);
    }

    private async Task<int> CountSessionsAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.UserSessions.CountAsync(s => s.User.Email == email);
    }

    private async Task<DateTimeOffset> GetLastSessionExpiryAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.UserSessions
            .Where(s => s.User.Email == email)
            .OrderByDescending(s => s.SessionId)
            .Select(s => s.ExpiresAt)
            .FirstAsync();
    }

    private async Task<int> CountUsersAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.CountAsync(u => u.Email == email);
    }

    private async Task<string> GetPasswordHashAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync();
    }
}