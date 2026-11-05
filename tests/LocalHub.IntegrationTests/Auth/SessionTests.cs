using System.Net;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class SessionTests(LocalHubApiFactory factory)
{
    private const string MeUrl = "/api/auth/me";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RNF-02")]
    public async Task ProtectedRequest_AfterEightHours_Returns401()
    {
        // Separate API with its own clock, so moving the time does not affect the other tests.
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = api.CreateClient();
        string token = await RegisterAndLoginAsync(client, NewEmail());

        clock.Advance(TimeSpan.FromHours(8).Subtract(TimeSpan.FromSeconds(1)));
        using var beforeExpiry = await SendWithTokenAsync(client, HttpMethod.Get, MeUrl, token);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var atExpiry = await SendWithTokenAsync(client, HttpMethod.Get, MeUrl, token);

        Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, atExpiry.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-04")]
    [Trait("Requisito", "RNF-02")]
    public async Task ProtectedRequest_AfterLogout_Returns401()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail());

        using var logout = await SendWithTokenAsync(_client, HttpMethod.Post, "/api/auth/logout", token);
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, token);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    [Trait("Requisito", "RNF-02")]
    public async Task ProtectedRequest_AfterAccountDeactivation_Returns401()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email);

        // Deactivated in the database without revoking the session, so the 401 comes from the account status check.
        await DeactivateAsync(email);
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, MeUrl, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "https://tools.ietf.org/html/rfc9110#section-15.5.2", MeUrl);
    }

    private async Task DeactivateAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"User\" SET status = 'INACTIVE'::activation_status WHERE email = {email}");
    }
}