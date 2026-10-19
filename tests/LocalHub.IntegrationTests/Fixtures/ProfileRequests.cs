using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Fixtures;

// A provider with a professional profile, created through the API (E01, E02, E09 and E12).
public record ProviderWithProfile(string Token, int UserId, string Email, int ProfessionalProfileId, int ServiceCategoryId);

// Requests to the professional profile endpoints (F004) that several test classes use.
public static class ProfileRequests
{
    public const string ProfilesUrl = "/api/profiles";

    // Each test uses its own categories, so the tests do not depend on each other.
    public static async Task<int> CreateCategoryAsync(HttpClient client, string adminToken, string? name = null)
    {
        using var response = await SendJsonAsync(
            client, HttpMethod.Post, "/api/categories", adminToken, new { name = name ?? $"Teste Perfil {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("serviceCategoryId").GetInt32();
    }

    // A valid body of E12 with the address known by FakeGeocodingService.
    public static Dictionary<string, object?> ValidProfileBody(params int[] serviceCategoryIds) => new()
    {
        ["introduction"] = "Canalizador com equipa própria na área do Grande Porto.",
        ["contactDetails"] = "Telefone 220 000 000",
        ["experience"] = "15 anos em reparação de fugas.",
        ["serviceCategoryIds"] = serviceCategoryIds,
        ["address"] = FakeGeocodingService.KnownAddress,
        ["serviceRadiusKm"] = 15,
    };

    public static async Task<(string Token, int UserId, string Email)> RegisterProviderAsync(HttpClient client)
    {
        string email = NewEmail();
        using var register = await RegisterAsync(client, "Prestador Teste", email, ValidPassword, "PROVIDER");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var body = await register.Content.ReadFromJsonAsync<JsonElement>();

        string token = await LoginAndGetTokenAsync(client, email, ValidPassword);
        return (token, body.GetProperty("userId").GetInt32(), email);
    }

    public static async Task<ProviderWithProfile> CreateProviderWithProfileAsync(HttpClient client)
    {
        string adminToken = await LoginAsAdminAsync(client);
        int categoryId = await CreateCategoryAsync(client, adminToken);
        var (token, userId, email) = await RegisterProviderAsync(client);

        using var response = await SendJsonAsync(client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(categoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryId);
    }

    public static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client, HttpMethod method, string url, string? token, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }
}