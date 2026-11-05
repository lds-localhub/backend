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

namespace LocalHub.IntegrationTests.Reports;

// F013: E33 (activity report of the client) and F014: E34 (activity report of the provider). Each test uses a new client
// or provider, so the report only has its data. The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ReportsEndpointsTests(LocalHubApiFactory factory)
{
    private const string ReportUrl = "/api/reports/client";
    private const string ProviderReportUrl = "/api/reports/provider";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- RF-F013-01: criterion 1 ----------

    // One ASSIGNED, one COMPLETED and one CANCELLED after being assigned; an OPEN service, with a pending proposal, and
    // a service cancelled while OPEN are not contracted.
    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task ClientReport_ThreeContractedServices_CountsOnePerStateAndIgnoresUnassigned()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        await CreateContractedAsync(client, provider, 80.00m);
        int completed = await CreateContractedAsync(client, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{completed}/complete");
        int cancelled = await CreateContractedAsync(client, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelled}/cancel");
        int open = await CreatePublicServiceAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(open, provider, 80.00m);
        int cancelledOpen = await CreatePublicServiceAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(cancelledOpen, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelledOpen}/cancel");

        var report = await GetReportAsync(client.Token);

        Assert.Equal(3, report.GetProperty("contractedServices").GetInt32());
        var byStatus = report.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("status").GetString()!, s => s.GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["ASSIGNED"] = 1, ["COMPLETED"] = 1, ["CANCELLED"] = 1 }, byStatus);
    }

    // ---------- RF-F013-01: rule 5 ----------

    [Fact]
    [Trait("Requisito", "RF-F013-01")]
    public async Task ClientReport_OtherClientServices_AreNotIncluded()
    {
        var provider = await CreateProviderAsync();
        var other = await RegisterClientAsync(_client);
        await CreateContractedAsync(other, provider, 80.00m);
        var client = await RegisterClientAsync(_client);

        var report = await GetReportAsync(client.Token);

        Assert.Equal(0, report.GetProperty("contractedServices").GetInt32());
        Assert.Empty(report.GetProperty("byCategory").EnumerateArray());
        Assert.All(report.GetProperty("byStatus").EnumerateArray(), s => Assert.Equal(0, s.GetProperty("count").GetInt32()));
        Assert.Equal(0m, report.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Empty(report.GetProperty("completedHistory").EnumerateArray());
    }

    // ---------- RF-F013-02 ----------

    // Criteria 1 and 3: the day of creation is the day in UTC (API documentation v04, section 4.3), so a service created
    // at 2026-09-30T23:30Z belongs to September, although it is already 1 October in Portugal.
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public async Task ClientReport_OctoberPeriod_CountsOnlyServicesCreatedThatMonthInUtc()
    {
        var provider = await CreateProviderAsync();
        var client = await RegisterClientAsync(_client);
        int september = await CreateContractedAsync(client, provider, 10.00m);
        int firstOfOctober = await CreateContractedAsync(client, provider, 20.00m);
        int lastOfOctober = await CreateContractedAsync(client, provider, 40.00m);
        int november = await CreateContractedAsync(client, provider, 80.00m);
        await SetCreationDateAsync(september, new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero));
        await SetCreationDateAsync(firstOfOctober, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await SetCreationDateAsync(lastOfOctober, new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        await SetCreationDateAsync(november, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        var october = await GetReportAsync(client.Token, "?startDate=2026-10-01&endDate=2026-10-31");
        var all = await GetReportAsync(client.Token);

        Assert.Equal(2, october.GetProperty("contractedServices").GetInt32());
        Assert.Equal(60.00m, october.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Equal(4, all.GetProperty("contractedServices").GetInt32());
        Assert.False(october.TryGetProperty("period", out _));
    }

    // ---------- E34, RF-F014-01: rules 1 to 4 ----------

    // Rule 1: only the COMPLETED ones are completed services. Rule 2: the distributions have all the services in which
    // the proposal of the provider was accepted, and not the service that the client gave to another provider.
    [Fact]
    [Trait("Requisito", "RF-F014-01")]
    public async Task ProviderReport_ServicesWithAcceptedProposal_CountsCompletedAndGroupsByCategoryAndState()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int plumbing = await CreateCategoryAsync(_client, adminToken, $"Canalização {Guid.NewGuid():N}");
        int gardening = await CreateCategoryAsync(_client, adminToken, $"Jardinagem {Guid.NewGuid():N}");
        var provider = await CreateProviderInCategoriesAsync(plumbing, gardening);
        var other = await CreateProviderInCategoriesAsync(plumbing);
        var client = await RegisterClientAsync(_client);
        int completed = await CreateContractedAsync(client, provider with { ServiceCategoryId = plumbing }, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{completed}/complete");
        int cancelled = await CreateContractedAsync(client, provider with { ServiceCategoryId = plumbing }, 150.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{cancelled}/cancel");
        await CreateContractedAsync(client, provider with { ServiceCategoryId = gardening }, 20.00m);
        await CreateContractedAsync(client, other, 999.00m);

        var report = await GetProviderReportAsync(provider.Token);

        Assert.Equal(1, report.GetProperty("completedServices").GetInt32());
        Assert.Equal(250.00m, report.GetProperty("totalAcceptedAmount").GetDecimal());
        var byCategory = report.GetProperty("byCategory").EnumerateArray().ToList();
        Assert.Equal(2, byCategory.Count);
        Assert.Equal(plumbing, byCategory[0].GetProperty("serviceCategoryId").GetInt32());
        Assert.StartsWith("Canalização", byCategory[0].GetProperty("name").GetString());
        Assert.Equal(2, byCategory[0].GetProperty("count").GetInt32());
        Assert.Equal(gardening, byCategory[1].GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(1, byCategory[1].GetProperty("count").GetInt32());
        var byStatus = report.GetProperty("byStatus").EnumerateArray()
            .ToDictionary(s => s.GetProperty("status").GetString()!, s => s.GetProperty("count").GetInt32());
        Assert.Equal(new Dictionary<string, int> { ["ASSIGNED"] = 1, ["COMPLETED"] = 1, ["CANCELLED"] = 1 }, byStatus);
    }

    // ---------- E34, RF-F014-02 ----------

    // Criterion 1 and rule 2: the direct requests, the proposals and the accepted amounts follow the creation date of
    // the service, in UTC, although they were created later.
    [Fact]
    [Trait("Requisito", "RF-F014-02")]
    public async Task ProviderReport_OctoberPeriod_CountsOnlyServicesCreatedThatMonthAndTheirData()
    {
        var provider = await CreateProviderAsync();
        var other = await CreateProviderInCategoriesAsync(provider.ServiceCategoryId);
        var client = await RegisterClientAsync(_client);

        int september = await CreateContractedAsync(client, provider, 10.00m);
        int firstOfOctober = await CreateContractedAsync(client, provider, 20.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{firstOfOctober}/complete");
        int november = await CreateContractedAsync(client, provider, 40.00m);

        int declinedInOctober = await CreatePublicServiceAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(declinedInOctober, provider, 90.00m);
        int chosen = await SubmitAsync(declinedInOctober, other, 70.00m);
        await PostOkAsync(client.Token, $"/api/proposals/{chosen}/accept");
        int declinedInSeptember = await CreatePublicServiceAsync(client, provider.ServiceCategoryId);
        await SubmitAsync(declinedInSeptember, provider, 90.00m);
        chosen = await SubmitAsync(declinedInSeptember, other, 70.00m);
        await PostOkAsync(client.Token, $"/api/proposals/{chosen}/accept");

        int directInOctober = await CreateDirectServiceAsync(client, provider);
        int directInSeptember = await CreateDirectServiceAsync(client, provider);

        var lastOfSeptember = new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero);
        var firstDay = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        await SetCreationDateAsync(september, lastOfSeptember);
        await SetCreationDateAsync(firstOfOctober, firstDay);
        await SetCreationDateAsync(november, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        await SetCreationDateAsync(declinedInOctober, new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        await SetCreationDateAsync(declinedInSeptember, lastOfSeptember);
        await SetCreationDateAsync(directInOctober, firstDay);
        await SetCreationDateAsync(directInSeptember, lastOfSeptember);

        var october = await GetProviderReportAsync(provider.Token, "?startDate=2026-10-01&endDate=2026-10-31");
        var all = await GetProviderReportAsync(provider.Token);

        Assert.Equal(1, october.GetProperty("completedServices").GetInt32());
        Assert.Equal(1, october.GetProperty("byCategory")[0].GetProperty("count").GetInt32());
        Assert.Equal(1, october.GetProperty("directRequestsReceived").GetInt32());
        Assert.Equal(1, october.GetProperty("proposals").GetProperty("accepted").GetInt32());
        Assert.Equal(1, october.GetProperty("proposals").GetProperty("declined").GetInt32());
        Assert.Equal(20.00m, october.GetProperty("totalAcceptedAmount").GetDecimal());
        Assert.Equal(3, all.GetProperty("proposals").GetProperty("accepted").GetInt32());
        Assert.Equal(2, all.GetProperty("proposals").GetProperty("declined").GetInt32());
        Assert.Equal(2, all.GetProperty("directRequestsReceived").GetInt32());
        Assert.False(october.TryGetProperty("period", out _));
    }

    // ---------- Helpers ----------

    private async Task<JsonElement> GetReportAsync(string token, string query = "")
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ReportUrl + query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetProviderReportAsync(string token, string query = "")
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, ProviderReportUrl + query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> PostOkAsync(string token, string url)
    {
        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // A provider with a profile in a new category, optionally with the given name.
    private async Task<ProviderWithProfile> CreateProviderAsync(string? categoryName = null)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        int categoryId = await CreateCategoryAsync(_client, adminToken, categoryName);
        return await CreateProviderInCategoriesAsync(categoryId);
    }

    // A provider with a profile in existing categories; ServiceCategoryId is the first one.
    private async Task<ProviderWithProfile> CreateProviderInCategoriesAsync(params int[] categoryIds)
    {
        var (token, userId, email) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(categoryIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), categoryIds[0]);
    }

    private async Task<int> CreatePublicServiceAsync(TestClient client, int serviceCategoryId)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    private async Task<int> SubmitAsync(int serviceId, ProviderWithProfile provider, decimal amount)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
            provider.Token, new { amount, conditions = "Material incluído." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();
    }

    // A PUBLIC service of the client with the proposal of the provider accepted (ASSIGNED).
    private async Task<int> CreateContractedAsync(TestClient client, ProviderWithProfile provider, decimal amount)
    {
        int serviceId = await CreatePublicServiceAsync(client, provider.ServiceCategoryId);
        int proposalId = await SubmitAsync(serviceId, provider, amount);
        await PostOkAsync(client.Token, $"/api/proposals/{proposalId}/accept");
        return serviceId;
    }

    // A contracted service, completed and reviewed by the client, with the review moved to the given date.
    private async Task CreateReviewedAsync(TestClient client, ProviderWithProfile provider, int rating, DateTimeOffset date)
    {
        int serviceId = await CreateContractedAsync(client, provider, 80.00m);
        await PostOkAsync(client.Token, $"{ServicesUrl}/{serviceId}/complete");
        using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/review",
            client.Token, new { rating });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Review\" SET \"creationDate\" = {date} WHERE \"serviceId\" = {serviceId}");
    }

    // The same, returning the identifier of the service.
    private async Task<int> CreateDirectServiceAsync(TestClient client, ProviderWithProfile provider)
    {
        var service = await PostDirectServiceAsync(client, provider);
        return service.GetProperty("serviceId").GetInt32();
    }

    private async Task<JsonElement> PostDirectServiceAsync(TestClient client, ProviderWithProfile provider)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token,
            DirectRequestBody(provider.ServiceCategoryId, provider.ProfessionalProfileId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // The creation date comes from the clock of the API, so the services of other months are moved in the database.
    private async Task SetCreationDateAsync(int serviceId, DateTimeOffset creationDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Service\" SET \"creationDate\" = {creationDate} WHERE \"serviceId\" = {serviceId}");
    }
}