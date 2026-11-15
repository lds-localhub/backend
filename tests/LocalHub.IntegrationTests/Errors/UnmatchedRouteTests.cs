using System.Net;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Errors;

// Requests that match no endpoint also get a problem+json body, so the frontends can always read "detail".
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class UnmatchedRouteTests(LocalHubApiFactory factory)
{
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string MethodNotAllowedType = "https://tools.ietf.org/html/rfc9110#section-15.5.6";

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [Trait("Requisito", "RNF-02")]
    [InlineData("/api/nao-existe")]
    [InlineData("/api/services/abc")]
    public async Task UnknownRoute_WithSession_Returns404Problem(string url)
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await AssertProblemAsync(response, NotFoundType, url);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task WrongMethod_WithSession_Returns405Problem()
    {
        const string url = "/api/auth/me";
        string token = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        using var response = await SendWithTokenAsync(_client, HttpMethod.Delete, url, token);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var body = await AssertProblemAsync(response, MethodNotAllowedType, url);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
    }

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task UnknownRoute_WithoutSession_Returns401Problem()
    {
        const string url = "/api/nao-existe";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, token: null);

        // Without a session the API does not reveal which routes exist.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.2", url);
    }
}