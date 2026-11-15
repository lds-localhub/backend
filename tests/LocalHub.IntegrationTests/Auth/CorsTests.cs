using System.Net;

using LocalHub.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LocalHub.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class CorsTests(LocalHubApiFactory factory)
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string LoginUrl = "/api/auth/login";
    private const string MeUrl = "/api/auth/me";

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task Preflight_FromAllowedOrigin_WithoutToken_IsAccepted()
    {
        using var api = WithAllowedOrigin();
        using var client = api.CreateClient();

        // Protected endpoint: the preflight carries no token and must not get a 401.
        using var response = await SendPreflightAsync(client, MeUrl, AllowedOrigin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("authorization", AllowedHeaders(response), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task Preflight_FromOtherOrigin_HasNoAllowOriginHeader()
    {
        using var api = WithAllowedOrigin();
        using var client = api.CreateClient();

        using var response = await SendPreflightAsync(client, LoginUrl, "http://outro-site.test");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task Request_FromAllowedOrigin_HasAllowOriginHeader()
    {
        using var api = WithAllowedOrigin();
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task Preflight_WithoutConfiguredOrigins_HasNoAllowOriginHeader()
    {
        // The default configuration has no allowed origins.
        using var client = factory.CreateClient();

        using var response = await SendPreflightAsync(client, LoginUrl, AllowedOrigin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private WebApplicationFactory<Program> WithAllowedOrigin() =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin));

    private static async Task<HttpResponseMessage> SendPreflightAsync(HttpClient client, string url, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await client.SendAsync(request);
    }

    private static IEnumerable<string> AllowedHeaders(HttpResponseMessage response) =>
        response.Headers.GetValues("Access-Control-Allow-Headers")
            .SelectMany(v => v.Split(',', StringSplitOptions.TrimEntries));
}