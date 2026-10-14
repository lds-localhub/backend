using System.Net;

using LocalHub.IntegrationTests.Fixtures;

namespace LocalHub.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class ApiSmokeTests(LocalHubApiFactory factory)
{
    [Fact]
    public async Task Api_StartsAndServesHealthCheck()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}