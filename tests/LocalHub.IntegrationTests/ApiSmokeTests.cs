using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace LocalHub.IntegrationTests;

public class ApiSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Api_StartsAndServesOpenApiDocument()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}