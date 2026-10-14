using System.Net;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Auth;

// Cross-cutting access tests (test plan, section 5.3): every protected endpoint rejects requests without a valid session.
// No endpoint of F001 is restricted to a role, so there are no 403 cases yet.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AccessControlTests(LocalHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // Add each new protected endpoint here.
    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "POST", "/api/auth/logout" },
        { "GET", "/api/users/me" },
        { "PUT", "/api/users/me/location" },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task EndpointProtegido_SemToken_Devolve401(string method, string url)
    {
        using var response = await SendWithTokenAsync(_client, new HttpMethod(method), url, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.2", url);
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task EndpointProtegido_TokenInvalido_Devolve401(string method, string url)
    {
        using var response = await SendWithTokenAsync(_client, new HttpMethod(method), url, "abc.def.ghi");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("POST", "/api/auth/login")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task EndpointPublico_SemToken_NaoDevolve401(string method, string url)
    {
        // An empty body is enough: the request must reach the validation (400), not be stopped by authentication.
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.1", url);
    }
}