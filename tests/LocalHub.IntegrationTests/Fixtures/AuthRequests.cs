using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalHub.IntegrationTests.Fixtures;

// Requests to the authentication endpoints that several test classes use.
public static class AuthRequests
{
    public const string ValidPassword = "casa2026";

    // Administrator created by the DataSeeder, with the password LocalHubApiFactory.SeedPassword.
    public const string AdminEmail = "ana.almeida@localhub.test";

    // Each test uses its own e-mail, so the tests do not depend on each other.
    public static string NewEmail() => $"teste.{Guid.NewGuid():N}@localhub.test";

    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string name, string email, string password, string role) =>
        client.PostAsJsonAsync("/api/auth/register", new { name, email, password, role });

    // "app" is the application that starts the session (E02, field client): WEB or MOBILE.
    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string email, string password, string app = "WEB") =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password, client = app });

    public static async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpClient client, HttpMethod method, string url, string? token)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    // Checks the "type" and "instance" fields of a Problem Details response (API documentation, section 4.6).
    public static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, string type, string instance)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(type, body.GetProperty("type").GetString());
        Assert.Equal(instance, body.GetProperty("instance").GetString());
        Assert.Equal((int)response.StatusCode, body.GetProperty("status").GetInt32());
        return body;
    }

    public static Task<string> LoginAsAdminAsync(HttpClient client) =>
        LoginAndGetTokenAsync(client, AdminEmail, LocalHubApiFactory.SeedPassword);

    public static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        using var login = await LoginAsync(client, email, password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    public static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string role = "PROVIDER")
    {
        using var register = await RegisterAsync(client, "Teste", email, ValidPassword, role);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        return await LoginAndGetTokenAsync(client, email, ValidPassword);
    }
}