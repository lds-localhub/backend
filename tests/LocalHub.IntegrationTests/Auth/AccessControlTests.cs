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
        { "GET", "/api/auth/me" },
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
        { "POST", "/api/services" },
        { "GET", "/api/services/1" },
        { "POST", "/api/services/1/proposals" },
        { "GET", "/api/proposals" },
        { "GET", "/api/direct-requests" },
        { "POST", "/api/direct-requests/1/decline" },
        { "GET", "/api/search/services" },
        { "GET", "/api/search/services/1" },
        { "GET", "/api/direct-requests/1" },
        { "GET", "/api/proposals/1" },
        { "GET", "/api/services" },
        { "POST", "/api/proposals/1/accept" },
        { "POST", "/api/proposals/1/decline" },
        { "POST", "/api/services/1/cancel" },
        { "POST", "/api/services/1/complete" },
        { "POST", "/api/services/1/review" },
        { "GET", "/api/profiles/1/reviews" },
        { "GET", "/api/reports/client" },
        { "GET", "/api/reports/provider" },
        { "PUT", "/api/users/me/password" },
        { "POST", "/api/accounts/1/reset-password" },
        { "GET", "/api/admin/summary" },
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
        { "POST", "/api/accounts/1/reset-password", "CLIENT" },
        { "POST", "/api/accounts/1/reset-password", "PROVIDER" },
        { "GET", "/api/admin/summary", "CLIENT" },
        { "GET", "/api/admin/summary", "PROVIDER" },
    };

    // Add each endpoint restricted to the client or the provider here, with one role that is not authorized.
    public static TheoryData<string, string, string> RoleEndpoints => new()
    {
        { "POST", "/api/profiles", "CLIENT" },
        { "GET", "/api/profiles/1", "ADMIN" },
        { "PUT", "/api/profiles/me", "CLIENT" },
        { "GET", "/api/profiles/1/reviews", "ADMIN" },
        { "PUT", "/api/users/me/location", "PROVIDER" },
        { "POST", "/api/services", "PROVIDER" },
        { "GET", "/api/services", "PROVIDER" },
        { "GET", "/api/services/1", "PROVIDER" },
        { "POST", "/api/services/1/cancel", "ADMIN" },
        { "POST", "/api/services/1/complete", "PROVIDER" },
        { "POST", "/api/services/1/review", "ADMIN" },
        { "POST", "/api/services/1/proposals", "CLIENT" },
        { "GET", "/api/proposals", "CLIENT" },
        { "GET", "/api/proposals/1", "CLIENT" },
        { "POST", "/api/proposals/1/accept", "PROVIDER" },
        { "POST", "/api/proposals/1/decline", "PROVIDER" },
        { "GET", "/api/direct-requests", "CLIENT" },
        { "GET", "/api/direct-requests/1", "CLIENT" },
        { "POST", "/api/direct-requests/1/decline", "CLIENT" },
        { "GET", "/api/search/services", "ADMIN" },
        { "GET", "/api/search/services/1", "CLIENT" },
        { "GET", "/api/reports/client", "PROVIDER" },
        { "GET", "/api/reports/provider", "CLIENT" },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401(string method, string url)
    {
        using var response = await SendWithTokenAsync(_client, new HttpMethod(method), url, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.2", url);
    }

    [Theory]
    [MemberData(nameof(AdministratorEndpoints))]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RF-F002-01")]
    [Trait("Requisito", "RNF-03")]
    public async Task AdministratorEndpoint_UnauthorizedRole_Returns403(string method, string url, string role)
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await SendWithTokenAsync(_client, new HttpMethod(method), url, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.4", url);
    }

    [Theory]
    [MemberData(nameof(RoleEndpoints))]
    [Trait("Requisito", "RF-F001-03")]
    [Trait("Requisito", "RNF-03")]
    public async Task ClientOrProviderEndpoint_UnauthorizedRole_Returns403(string method, string url, string role)
    {
        string token = role == "ADMIN"
            ? await LoginAsAdminAsync(_client)
            : await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await SendWithTokenAsync(_client, new HttpMethod(method), url, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.4", url);
    }
}