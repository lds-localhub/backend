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

namespace LocalHub.IntegrationTests.Services;

// F007 and F011, provider side: E25 (submit a proposal), E24 (decline a direct request), E27 (my proposals), E23 (my
// direct requests) and E22 (a service as the provider sees it). The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ProposalsEndpointsTests(LocalHubApiFactory factory)
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";
    private const string InvalidTransitionType = "https://localhub.local/erros/transicao-invalida";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E25: criteria 1 and 2 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-01")]
    public async Task SubmitProposal_RecipientOfPendingDirectRequest_Returns201AndDirectRequestBecomesProposalSubmitted()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);

        using var response = await SubmitAsync(provider.Token, serviceId, ValidProposal());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = await response.Content.ReadFromJsonAsync<JsonElement>();
        int proposalId = proposal.GetProperty("proposalId").GetInt32();
        Assert.Equal($"/api/proposals/{proposalId}", response.Headers.Location?.OriginalString);
        // Only the fields of the proposal (API documentation, E25): the service and the profile are those of the request.
        Assert.False(proposal.TryGetProperty("serviceId", out _));
        Assert.False(proposal.TryGetProperty("professionalProfileId", out _));
        Assert.Equal(80.00m, proposal.GetProperty("amount").GetDecimal());
        Assert.Equal("Inclui deslocação e material.", proposal.GetProperty("conditions").GetString());
        Assert.Equal("Disponível dia 9 às 18h30.", proposal.GetProperty("obs").GetString());
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
        Assert.EndsWith("Z", proposal.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, proposal.GetProperty("decisionDate").ValueKind);

        // The request was answered with this proposal (E36).
        using var detail = await GetAsync(provider.Token, $"/api/direct-requests/{requests[provider.ProfessionalProfileId]}");
        var request = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PROPOSAL_SUBMITTED", request.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, request.GetProperty("responseDate").ValueKind);
        Assert.Equal(proposalId, request.GetProperty("proposalId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_OpenPublicService_Returns201WithPendingProposalAndNoNotes()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);

        using var response = await SubmitAsync(provider.Token, serviceId, new { amount = 150.00m, conditions = "Material incluído." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, proposal.GetProperty("obs").ValueKind);
    }

    // ---------- E25: criterion 3 (validation) ----------

    [Theory]
    [InlineData("amount", "0")]
    [InlineData("amount", "12.345")]
    [InlineData("amount", "100000.01")]
    [InlineData("amount", null)]
    [InlineData("obs", "501")]
    [Trait("Requisito", "RF-F007-01")]
    [Trait("Requisito", "RF-F011-02")]
    public async Task SubmitProposal_InvalidField_Returns400OnFieldAndCreatesNoProposal(string field, string? value)
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId) = await CreatePublicServiceAsync(provider.ServiceCategoryId);
        var body = ValidProposal();
        body[field] = field switch
        {
            "amount" when value != null => decimal.Parse(value, CultureInfo.InvariantCulture),
            "conditions" when value != null => new string('a', int.Parse(value, CultureInfo.InvariantCulture)),
            "obs" when value != null => new string('a', int.Parse(value, CultureInfo.InvariantCulture)),
            _ => null,
        };

        using var response = await SubmitAsync(provider.Token, serviceId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, $"{ServicesUrl}/{serviceId}/proposals");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Esperado um erro em errors.{field}.");
        Assert.Equal(0, await CountProposalsAsync(serviceId));
    }

    // ---------- E24 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_RecipientOfPendingDirectRequest_Returns200DeclinedAndServiceStaysOpen()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, _, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        int requestId = requests[provider.ProfessionalProfileId];

        using var response = await PostAsync(provider.Token, $"/api/direct-requests/{requestId}/decline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(requestId, body.GetProperty("directRequestId").GetInt32());
        Assert.Equal("DECLINED", body.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("responseDate").ValueKind);
        // Only what changed in the request (API documentation, E24).
        Assert.Equal(3, body.EnumerateObject().Count());

        // The provider sees the declined request and the service still OPEN in E36.
        using var detail = await GetAsync(provider.Token, $"/api/direct-requests/{requestId}");
        var seen = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DECLINED", seen.GetProperty("status").GetString());
        Assert.Equal("OPEN", seen.GetProperty("service").GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-02")]
    public async Task Decline_OtherProviderDirectRequest_Returns403()
    {
        var recipient = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(recipient.ServiceCategoryId);
        var (_, _, requests) = await CreateDirectServiceAsync(recipient.ServiceCategoryId, recipient.ProfessionalProfileId);
        string url = $"/api/direct-requests/{requests[recipient.ProfessionalProfileId]}/decline";

        using var response = await PostAsync(other.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    // ---------- E23 (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task ListDirectRequests_ReceivedDirectRequests_ReturnsOnlyOwnWithSummaryWithoutClientContacts()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, firstService, _) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId, other.ProfessionalProfileId);
        var (_, secondService, secondRequests) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId);
        using var submitted = await SubmitAsync(provider.Token, firstService, ValidProposal());
        int proposalId = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();

        using var response = await GetAsync(provider.Token, "/api/direct-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([secondService, firstService], items.Select(i => i.GetProperty("service").GetProperty("serviceId").GetInt32()));
        Assert.Equal(secondRequests[provider.ProfessionalProfileId], items[0].GetProperty("directRequestId").GetInt32());
        Assert.Equal("PENDING", items[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("proposalId").ValueKind);
        Assert.Equal("PROPOSAL_SUBMITTED", items[1].GetProperty("status").GetString());
        Assert.Equal(proposalId, items[1].GetProperty("proposalId").GetInt32());

        // Only the summary of the lists (API documentation, E23): the detail is consulted in E36.
        var service = items[0].GetProperty("service");
        Assert.Equal(provider.ServiceCategoryId, service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("Cliente Teste", service.GetProperty("clientName").GetString());
        Assert.False(service.TryGetProperty("description", out _));
        Assert.False(items[0].TryGetProperty("creationDate", out _));
        Assert.DoesNotContain("clientId", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ---------- Helpers ----------

    private static Dictionary<string, object?> ValidProposal() => new()
    {
        ["amount"] = 80.00m,
        ["conditions"] = "Inclui deslocação e material.",
        ["obs"] = "Disponível dia 9 às 18h30.",
    };

    private Task<HttpResponseMessage> SubmitAsync(string token, int serviceId, object body) =>
        SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals", token, body);

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    private Task<HttpResponseMessage> PostAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Post, url, token);

    // A service with direct requests to the given profiles (E20). Returns the client, the serviceId and the
    // directRequestId of each profile.
    private async Task<(TestClient Client, int ServiceId, Dictionary<int, int> Requests)> CreateDirectServiceAsync(
        int serviceCategoryId, params int[] professionalProfileIds)
    {
        var client = await RegisterClientAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, DirectRequestBody(serviceCategoryId, professionalProfileIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => r.GetProperty("professionalProfile").GetProperty("professionalProfileId").GetInt32(),
            r => r.GetProperty("directRequestId").GetInt32());
        return (client, service.GetProperty("serviceId").GetInt32(), requests);
    }

    private async Task<(TestClient Client, int ServiceId)> CreatePublicServiceAsync(int serviceCategoryId)
    {
        var client = await RegisterClientAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (client, service.GetProperty("serviceId").GetInt32());
    }

    // A second provider with a profile in the category.
    private async Task<ProviderWithProfile> CreateProviderInCategoryAsync(int serviceCategoryId)
    {
        var (token, userId, email) = await RegisterProviderAsync(_client);
        using var response = await SendJsonAsync(
            _client, HttpMethod.Post, ProfilesUrl, token, ValidProfileBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new ProviderWithProfile(
            token, userId, email, profile.GetProperty("professionalProfileId").GetInt32(), serviceCategoryId);
    }

    private async Task DeactivateAsync(int userId)
    {
        string adminToken = await LoginAsAdminAsync(_client);
        using var response = await PostAsync(adminToken, $"/api/accounts/{userId}/deactivate");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The endpoints that cancel or assign a service are later issues, so the state is changed in the database.
    private async Task CancelServiceAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE \"Service\" SET \"status\" = 'CANCELLED' WHERE \"serviceId\" = {serviceId}");
    }

    private async Task<int> CountProposalsAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Proposals.CountAsync(p => p.ServiceId == serviceId);
    }
}