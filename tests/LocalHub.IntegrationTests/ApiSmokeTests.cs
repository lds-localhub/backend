using System.Net;

using LocalHub.IntegrationTests.Fixtures;

namespace LocalHub.IntegrationTests;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ApiSmokeTests(LocalHubApiFactory factory)
{
    [Fact]
    public async Task Api_Arranque_DevolveHealthCheck200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}