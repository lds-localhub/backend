using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Search;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class OpenServicesEndpointsTests(LocalHubApiFactory factory)
{
    private const string Url = "/api/search/services";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RF-F002-02")]
    public async Task SearchServices_MixedServices_ReturnsOnlyOpenPublicServicesOfActiveClients()
    {
        var provider = await CreateProviderAsync(15);
        int eligible = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        int assigned = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await SetServiceStatusAsync(assigned, "ASSIGNED");
        int completed = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await SetServiceStatusAsync(completed, "COMPLETED");
        int cancelled = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await SetServiceStatusAsync(cancelled, "CANCELLED");
        var deactivatedClient = await RegisterClientAsync(_client);
        await CreatePublicServiceAsync(deactivatedClient, provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        await DeactivateAsync(deactivatedClient.UserId);
        // A service with direct requests to the provider itself is not a PUBLIC service either.
        await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);

        using var response = await GetAsync(provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}");

        var body = await BodyAsync(response);
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(eligible, item.GetProperty("serviceId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RNF-06")]
    public async Task SearchServices_ServicesAt2_10And30Km_ReturnsSortedByDistanceWithoutClientData()
    {
        var provider = await CreateProviderAsync(15);
        // Created out of order, so the order can only come from the distance.
        int at30 = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt30Km);
        int at2 = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt2Km);
        int at10 = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt10Km);

        using var response = await GetAsync(provider.Token, $"{Url}?serviceCategoryId={provider.ServiceCategoryId}");

        var body = await BodyAsync(response);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.GetProperty("total").GetInt32());
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([at2, at10, at30], ServiceIds(items));
        Assert.Equal([2.0m, 10.0m, 30.0m], items.Select(i => i.GetProperty("distanceKm").GetDecimal()));
        Assert.Equal([true, true, false], items.Select(i => i.GetProperty("withinServiceRadius").GetBoolean()));

        var first = items[0];
        Assert.Equal(provider.ServiceCategoryId,
            first.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("Substituir torneira", first.GetProperty("title").GetString());
        Assert.Equal("Substituir a torneira da cozinha que pinga.", first.GetProperty("description").GetString());
        Assert.Equal(FakeGeocodingService.AddressAt2Km, first.GetProperty("location").GetString());
        Assert.Equal(Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            first.GetProperty("requestedStartDate").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("requestedEndDate").ValueKind);
        Assert.EndsWith("Z", first.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        AssertHasNoClientData(first);
    }

    [Fact]
    [Trait("Requisito", "RF-F011-01")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetService_OpenPublicService_Returns200WithDetailWithoutClientData()
    {
        var provider = await CreateProviderAsync(15);
        int serviceId = await CreatePublicServiceAsync(provider.ServiceCategoryId, FakeGeocodingService.AddressAt10Km);

        using var response = await GetAsync(provider.Token, $"{Url}/{serviceId}");

        var service = await BodyAsync(response);
        Assert.Equal(serviceId, service.GetProperty("serviceId").GetInt32());
        Assert.Equal(provider.ServiceCategoryId, service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("Substituir torneira", service.GetProperty("title").GetString());
        Assert.Equal("Substituir a torneira da cozinha que pinga.", service.GetProperty("description").GetString());
        Assert.Equal(FakeGeocodingService.AddressAt10Km, service.GetProperty("location").GetString());
        Assert.Equal(10.0m, service.GetProperty("distanceKm").GetDecimal());
        Assert.True(service.GetProperty("withinServiceRadius").GetBoolean());
        Assert.Equal(Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            service.GetProperty("requestedStartDate").GetString());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("requestedEndDate").ValueKind);
        Assert.EndsWith("Z", service.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, service.GetProperty("myProposalId").ValueKind);
        AssertHasNoClientData(service);
        Assert.False(service.TryGetProperty("proposals", out _));
    }

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    private async Task<ProviderWithProfile> CreateProviderAsync(int serviceRadiusKm, int? serviceCategoryId = null)
    {
        int categoryId = serviceCategoryId ?? await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));
        var (token, userId, email) = await RegisterProviderAsync(_client);
        var body = ValidProfileBody(categoryId);
        body["serviceRadiusKm"] = serviceRadiusKm;
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryId);
    }

    private async Task<int> CreatePublicServiceAsync(int serviceCategoryId, string address) =>
        await CreatePublicServiceAsync(await RegisterClientAsync(_client), serviceCategoryId, address);

    private async Task<int> CreatePublicServiceAsync(TestClient client, int serviceCategoryId, string address)
    {
        var body = PublicBody(serviceCategoryId);
        body["location"] = address;
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task<int> CreateDirectServiceAsync(int serviceCategoryId, int professionalProfileId)
    {
        var client = await RegisterClientAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, DirectRequestBody(serviceCategoryId, professionalProfileId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task DeactivateAsync(int userId)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, $"/api/accounts/{userId}/deactivate", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SetServiceStatusAsync(int serviceId, string status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE "Service"
             SET "status" = {status}::service_status,
                 "actualCompletionDate" = CASE WHEN {status} = 'COMPLETED' THEN now() END
             WHERE "serviceId" = {serviceId}
             """);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<int> ServiceIds(List<JsonElement> items) =>
        items.Select(i => i.GetProperty("serviceId").GetInt32()).ToList();

    private static void AssertHasNoClientData(JsonElement service)
    {
        foreach (string field in new[] { "clientId", "client", "clientName", "email", "contactDetails" })
        {
            Assert.False(service.TryGetProperty(field, out _), $"O trabalho não pode ter o campo {field}.");
        }
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, Url);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Esperado um erro em errors.{field}.");
    }
}