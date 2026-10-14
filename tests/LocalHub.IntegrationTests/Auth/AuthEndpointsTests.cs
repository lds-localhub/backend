using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

namespace LocalHub.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public class AuthControllerTests(LocalHubApiFactory factory)
{
    private const string AdminEmail = "ana.almeida@localhub.test";
    private const string ValidPassword = "casa2026";

    private readonly HttpClient _client = factory.CreateClient();

    // Each test uses its own e-mail, so the tests do not depend on each other.
    private static string NewEmail() => $"teste.{Guid.NewGuid():N}@localhub.test";

    [Fact]
    public async Task Register_ValidClient_Returns201WithLocationAndActiveAccount()
    {
        string email = NewEmail();

        using var response = await RegisterAsync("Ana Costa", email, ValidPassword, "CLIENT");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/users/me", response.Headers.Location?.OriginalString);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("CLIENT", body.GetProperty("role").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Register_EmailAlreadyUsedWithDifferentCase_Returns409()
    {
        string email = NewEmail();
        using var first = await RegisterAsync("Ana Costa", email, ValidPassword, "CLIENT");

        using var response = await RegisterAsync("Ana Costa", email.ToUpperInvariant(), ValidPassword, "PROVIDER");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Register_InvalidFieldsAndAdminRole_Returns400WithErrorPerField()
    {
        using var response = await RegisterAsync("", "invalido", "curta", "ADMIN");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("role", out _));
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400WithRoleError()
    {
        using var response = await RegisterAsync("Ana Costa", NewEmail(), ValidPassword, "XYZ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("role", out _));
    }

    [Fact]
    public async Task Login_SeededAdministrator_Returns200WithTokenAndEightHourSession()
    {
        using var response = await LoginAsync(AdminEmail, LocalHubApiFactory.SeedPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("ADMIN", body.GetProperty("user").GetProperty("role").GetString());

        var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expiresAt - DateTimeOffset.UtcNow, TimeSpan.FromHours(7.9), TimeSpan.FromHours(8));
    }

    [Fact]
    public async Task Login_EmailWithDifferentCase_Returns200()
    {
        using var response = await LoginAsync(AdminEmail.ToUpperInvariant(), LocalHubApiFactory.SeedPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_Returns401WithSameMessage()
    {
        using var wrongPassword = await LoginAsync(AdminEmail, "errada123");
        using var unknownEmail = await LoginAsync(NewEmail(), "errada123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var first = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var second = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        using var response = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithInvalidToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithToken_ReturnsAuthenticatedUser()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(email);

        using var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("PROVIDER", body.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("latitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("professionalProfileId").ValueKind);
    }

    [Fact]
    public async Task Logout_Returns204AndTheTokenIsRejectedAfterwards()
    {
        string token = await RegisterAndLoginAsync(NewEmail());

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var logoutResponse = await _client.SendAsync(logout);
        using var meResponse = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    private Task<HttpResponseMessage> RegisterAsync(string name, string email, string password, string role) =>
        _client.PostAsJsonAsync("/api/auth/register", new { name, email, password, role });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<HttpResponseMessage> GetMeAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        using var register = await RegisterAsync("Teste", email, ValidPassword, "PROVIDER");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await LoginAsync(email, ValidPassword);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}