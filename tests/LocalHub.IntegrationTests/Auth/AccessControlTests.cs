using System.Net;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Auth;

// Cross-cutting access tests (test plan, section 5.3): every protected endpoint rejects requests without a valid session
// with 401, and requests of a role that is not authorized with 403.
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
        { "GET", "/api/accounts" },
        { "POST", "/api/accounts/1/deactivate" },
        { "POST", "/api/accounts/1/activate" },
        { "POST", "/api/categories" },
        { "PUT", "/api/categories/1" },
        { "POST", "/api/categories/1/deactivate" },
        { "PUT", "/api/users/me/location" },
        { "POST", "/api/profiles" },
        { "GET", "/api/profiles/1" },
        { "PUT", "/api/profiles/me" },
        { "PUT", "/api/profiles/me/service-radius" },
        { "POST", "/api/services" },
        { "GET", "/api/services/1" },
        { "POST", "/api/services/1/proposals" },
        { "GET", "/api/proposals" },
        { "GET", "/api/direct-requests" },
        { "POST", "/api/direct-requests/1/decline" },
        { "GET", "/api/services" },
        { "GET", "/api/services/1/proposals" },
        { "POST", "/api/services/1/proposals/1/accept" },
        { "POST", "/api/services/1/proposals/1/decline" },
    };

    // Add each endpoint restricted to the administrator here, once for each role that is not authorized.
    public static TheoryData<string, string, string> AdministratorEndpoints => new()
    {
        { "GET", "/api/accounts", "CLIENT" },
        { "GET", "/api/accounts", "PROVIDER" },
        { "POST", "/api/accounts/1/deactivate", "CLIENT" },
        { "POST", "/api/accounts/1/deactivate", "PROVIDER" },
        { "POST", "/api/accounts/1/activate", "CLIENT" },
        { "POST", "/api/accounts/1/activate", "PROVIDER" },
        { "POST", "/api/categories", "CLIENT" },
        { "POST", "/api/categories", "PROVIDER" },
        { "PUT", "/api/categories/1", "CLIENT" },
        { "PUT", "/api/categories/1", "PROVIDER" },
        { "POST", "/api/categories/1/deactivate", "CLIENT" },
        { "POST", "/api/categories/1/deactivate", "PROVIDER" },
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
    [MemberData(nameof(AdministratorEndpoints))]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RF-F002-01")]
    [Trait("Requisito", "RNF-03")]
    public async Task EndpointDeAdministrador_PapelNaoAutorizado_Devolve403(string method, string url, string role)
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await SendWithTokenAsync(_client, new HttpMethod(method), url, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.4", url);
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

    // E08 without includeInactive is public: the list of active categories (RF-F001-03, rule 3; RF-F003-04).
    [Fact]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RF-F003-04")]
    public async Task ListarCategoriasAtivas_SemToken_Devolve200()
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/categories", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // E18 is public (RF-F001-03, rule 3; RF-F005-01, rule 1): without parameters the request reaches the validation
    // (400, no category), not the authentication.
    [Fact]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RF-F005-01")]
    public async Task PesquisarPrestadores_SemToken_NaoDevolve401()
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/search/providers", token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "/api/search/providers");
    }
}