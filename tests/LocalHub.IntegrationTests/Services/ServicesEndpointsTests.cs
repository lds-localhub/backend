using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;
using static LocalHub.IntegrationTests.Fixtures.UserLocations;

namespace LocalHub.IntegrationTests.Services;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ServicesEndpointsTests(LocalHubApiFactory factory)
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string UnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F009-03")]
    public async Task Create_DirectRequestsToTwoEligibleProviders_Returns201WithOpenServiceAndTwoPendingDirectRequests()
    {
        var first = await CreateProviderWithProfileAsync(_client);
        var second = await CreateProviderInCategoryAsync(first.ServiceCategoryId);
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(first.ServiceCategoryId, first.ProfessionalProfileId, second);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        int serviceId = service.GetProperty("serviceId").GetInt32();
        Assert.Equal($"{ServicesUrl}/{serviceId}", response.Headers.Location?.OriginalString);
        Assert.Equal("Cliente Teste", service.GetProperty("clientName").GetString());
        Assert.False(service.TryGetProperty("clientId", out _));
        Assert.Equal("DIRECT_REQUEST", service.GetProperty("proposalModel").GetString());
        Assert.Equal(first.ServiceCategoryId,
            service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.False(string.IsNullOrEmpty(service.GetProperty("serviceCategory").GetProperty("name").GetString()));
        Assert.Equal("Torneira da casa de banho com fuga de água.", service.GetProperty("description").GetString());
        Assert.Equal(Iso(Today.AddDays(1)), service.GetProperty("requestedStartDate").GetString());
        Assert.Equal(Iso(Today.AddDays(3)), service.GetProperty("requestedEndDate").GetString());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("actualCompletionDate").ValueKind);
        Assert.EndsWith("Z", service.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.False(service.GetProperty("reviewed").GetBoolean());
        Assert.Empty(service.GetProperty("history").EnumerateArray());
        Assert.Empty(service.GetProperty("proposals").EnumerateArray());

        var requests = service.GetProperty("directRequests").EnumerateArray().ToList();
        Assert.Equal(
            new[] { first.ProfessionalProfileId, second }.Order(),
            requests.Select(r => r.GetProperty("professionalProfile").GetProperty("professionalProfileId").GetInt32()).Order());
        Assert.All(requests, r =>
        {
            Assert.True(r.GetProperty("directRequestId").GetInt32() > 0);
            Assert.Equal("PENDING", r.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("responseDate").ValueKind);
            Assert.Equal(JsonValueKind.Null, r.GetProperty("proposal").ValueKind);
        });
        Assert.Equal((1, 2), await CountSavedAsync(client.UserId));

        // Instants are returned to the second, so the returned date matches the stored one.
        string returned = service.GetProperty("creationDate").GetString()!;
        var saved = await ReadCreationDateAsync(serviceId);
        Assert.Equal(saved.AddTicks(-(saved.Ticks % TimeSpan.TicksPerSecond)),
            DateTimeOffset.Parse(returned, CultureInfo.InvariantCulture));
        using var detail = await SendWithTokenAsync(_client, HttpMethod.Get, $"{ServicesUrl}/{serviceId}", client.Token);
        var consulted = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(returned, consulted.GetProperty("creationDate").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_PublicService_Returns201OpenWithEmptyDirectRequests()
    {
        int categoryId = await NewCategoryAsync();
        var client = await RegisterClientAsync(_client);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(categoryId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PUBLIC", service.GetProperty("proposalModel").GetString());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("requestedEndDate").ValueKind);
        Assert.Empty(service.GetProperty("directRequests").EnumerateArray());
        Assert.Equal((1, 0), await CountSavedAsync(client.UserId));
    }

    [Fact]
    [Trait("Requisito", "RF-F010-01")]
    public async Task Create_SeveralInvalidFields_Returns400WithErrorPerField()
    {
        var client = await RegisterClientAsync(_client);
        var body = new Dictionary<string, object?> { ["proposalModel"] = "PUBLIC", ["description"] = "" };

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await AssertProblemAsync(response, BadRequestType, ServicesUrl)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("serviceCategoryId", out _));
        Assert.True(errors.TryGetProperty("description", out _));
        Assert.True(errors.TryGetProperty("requestedStartDate", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F009-01")]
    [Trait("Requisito", "RF-F015-03")]
    public async Task Create_GeocodingUnavailable_Returns503AndCreatesNoServiceOrDirectRequests()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        body["location"] = $"Rua das Flores 1, Porto {FakeGeocodingService.UnavailableMarker}";

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemAsync(response, UnavailableType, ServicesUrl);
        Assert.Equal((0, 0), await CountSavedAsync(client.UserId));
        Assert.Equal(0, await CountDirectRequestsAsync(provider.ProfessionalProfileId));
    }

    private static string Iso(DateOnly date) => date.ToString(IsoDate, CultureInfo.InvariantCulture);

    private async Task<int> NewCategoryAsync() => await CreateCategoryAsync(_client, await LoginAsAdminAsync(_client));

    private async Task<int> CreateProviderInCategoryAsync(int serviceCategoryId, bool deactivate = false)
    {
        var (token, userId, _) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (deactivate)
        {
            string adminToken = await LoginAsAdminAsync(_client);
            using var deactivated = await SendWithTokenAsync(
                _client, HttpMethod.Post, $"/api/accounts/{userId}/deactivate", adminToken);
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        }

        return profile.GetProperty("professionalProfileId").GetInt32();
    }

    private async Task<JsonElement> GetMeAsync(string token)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/auth/me", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<string>> AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, ServicesUrl);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var messages),
            $"Esperado um erro em errors.{field}.");
        return messages.EnumerateArray().Select(m => m.GetString()!).ToList();
    }

    private async Task<(int Services, int DirectRequests)> CountSavedAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        int services = await db.Services.CountAsync(s => s.ClientId == clientId);
        int directRequests = await db.DirectRequests.CountAsync(
            r => db.Services.Any(s => s.ServiceId == r.ServiceId && s.ClientId == clientId));
        return (services, directRequests);
    }

    private async Task<DateTimeOffset> ReadCreationDateAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Services.Where(s => s.ServiceId == serviceId).Select(s => s.CreationDate).SingleAsync();
    }

    private async Task<int> CountDirectRequestsAsync(int professionalProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.DirectRequests.CountAsync(r => r.ProfessionalProfileId == professionalProfileId);
    }
}