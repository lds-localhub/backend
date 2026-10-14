using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalHub.IntegrationTests.Fixtures;

// Requests to the authentication endpoints that several test classes use.
public static class AuthRequests
{
    public const string ValidPassword = "casa2026";

    // Each test uses its own e-mail, so the tests do not depend on each other.
    public static string NewEmail() => $"teste.{Guid.NewGuid():N}@localhub.test";

    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string name, string email, string password, string role) =>
        client.PostAsJsonAsync("/api/auth/register", new { name, email, password, role });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

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

    public static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string role = "PROVIDER")
    {
        using var register = await RegisterAsync(client, "Teste", email, ValidPassword, role);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await LoginAsync(client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}