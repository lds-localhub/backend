using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Services;

// F007 and F008, provider side: E36 (a direct request) and E37 (a proposal). The provider is at KnownAddress and the
// services at AddressAt10Km. The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ProviderDetailEndpointsTests(LocalHubApiFactory factory)
{
    private const string DirectRequestsUrl = "/api/direct-requests";
    private const string ProposalsUrl = "/api/proposals";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string MissingProfileType = "https://localhub.local/erros/perfil-em-falta";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E36 ----------

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetDirectRequest_Destinatario_Devolve200ComOServicoEONomeDoClienteSemContactos()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId, other.ProfessionalProfileId);
        int directRequestId = requests[provider.ProfessionalProfileId];

        using var response = await GetAsync(provider.Token, $"{DirectRequestsUrl}/{directRequestId}");

        var request = await BodyAsync(response);
        Assert.Equal(directRequestId, request.GetProperty("directRequestId").GetInt32());
        Assert.Equal("PENDING", request.GetProperty("status").GetString());
        Assert.EndsWith("Z", request.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, request.GetProperty("responseDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, request.GetProperty("proposalId").ValueKind);
        Assert.False(request.TryGetProperty("professionalProfileId", out _));

        var service = request.GetProperty("service");
        Assert.Equal(serviceId, service.GetProperty("serviceId").GetInt32());
        Assert.Equal(provider.ServiceCategoryId, service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("Torneira da casa de banho com fuga de água.", service.GetProperty("description").GetString());
        Assert.Equal(FakeGeocodingService.AddressAt10Km, service.GetProperty("location").GetString());
        Assert.Equal(10.0m, service.GetProperty("distanceKm").GetDecimal());
        Assert.Equal(Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            service.GetProperty("requestedStartDate").GetString());
        Assert.Equal(Today.AddDays(3).ToString(IsoDate, CultureInfo.InvariantCulture),
            service.GetProperty("requestedEndDate").GetString());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.Equal("Cliente Teste", service.GetProperty("clientName").GetString());
        Assert.Empty(service.GetProperty("history").EnumerateArray());
        AssertHasNoClientContacts(service);
        // Neither the requests nor the proposals of other providers (RNF-06).
        Assert.False(service.TryGetProperty("directRequests", out _));
        Assert.False(service.TryGetProperty("proposals", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task GetDirectRequest_DepoisDaProposta_DevolveOProposalIdEAPropostaApresentada()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        int proposalId = await SubmitProposalAsync(provider.Token, serviceId);

        using var response = await GetAsync(provider.Token, $"{DirectRequestsUrl}/{requests[provider.ProfessionalProfileId]}");

        var request = await BodyAsync(response);
        Assert.Equal("PROPOSAL_SUBMITTED", request.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, request.GetProperty("responseDate").ValueKind);
        Assert.Equal(proposalId, request.GetProperty("proposalId").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetDirectRequest_SolicitacaoDeOutroPrestador_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, _, requests) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId, other.ProfessionalProfileId);
        string url = $"{DirectRequestsUrl}/{requests[other.ProfessionalProfileId]}";

        using var response = await GetAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task GetDirectRequest_Inexistente_Devolve404()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string url = $"{DirectRequestsUrl}/{int.MaxValue}";

        using var response = await GetAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F007-09")]
    public async Task GetDirectRequest_ClienteDoServico_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (client, _, requests) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);

        using var response = await GetAsync(client.Token, $"{DirectRequestsUrl}/{requests[provider.ProfessionalProfileId]}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- E37 ----------

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetProposal_PrestadorQueAApresentou_Devolve200ComOServicoEONomeDoClienteSemContactos()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (_, serviceId, _) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        int proposalId = await SubmitProposalAsync(provider.Token, serviceId);

        using var response = await GetAsync(provider.Token, $"{ProposalsUrl}/{proposalId}");

        var proposal = await BodyAsync(response);
        Assert.Equal(proposalId, proposal.GetProperty("proposalId").GetInt32());
        Assert.Equal(80.00m, proposal.GetProperty("amount").GetDecimal());
        Assert.Equal("Inclui deslocação e material.", proposal.GetProperty("conditions").GetString());
        Assert.Equal("Disponível dia 9 às 18h30.", proposal.GetProperty("obs").GetString());
        Assert.Equal("PENDING", proposal.GetProperty("status").GetString());
        Assert.EndsWith("Z", proposal.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, proposal.GetProperty("decisionDate").ValueKind);

        var service = proposal.GetProperty("service");
        Assert.Equal(serviceId, service.GetProperty("serviceId").GetInt32());
        Assert.Equal("DIRECT_REQUEST", service.GetProperty("proposalModel").GetString());
        Assert.Equal(provider.ServiceCategoryId, service.GetProperty("serviceCategory").GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(FakeGeocodingService.AddressAt10Km, service.GetProperty("location").GetString());
        Assert.Equal(10.0m, service.GetProperty("distanceKm").GetDecimal());
        Assert.Equal("OPEN", service.GetProperty("status").GetString());
        Assert.EndsWith("Z", service.GetProperty("creationDate").GetString(), StringComparison.Ordinal);
        Assert.Equal("Cliente Teste", service.GetProperty("clientName").GetString());
        AssertHasNoClientContacts(service);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    public async Task GetProposal_TrabalhoPublicado_DevolveAModalidadePublic()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        using var create = await SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(provider.ServiceCategoryId));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        int serviceId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
        int proposalId = await SubmitProposalAsync(provider.Token, serviceId);

        using var response = await GetAsync(provider.Token, $"{ProposalsUrl}/{proposalId}");

        var service = (await BodyAsync(response)).GetProperty("service");
        Assert.Equal("PUBLIC", service.GetProperty("proposalModel").GetString());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("requestedEndDate").ValueKind);
        // The client created the job at its registered location, the same address as the provider.
        Assert.Equal(0.0m, service.GetProperty("distanceKm").GetDecimal());
    }

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    [Trait("Requisito", "RNF-06")]
    public async Task GetProposal_PropostaDeOutroPrestador_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderInCategoryAsync(provider.ServiceCategoryId);
        var (_, serviceId, _) = await CreateDirectServiceAsync(
            provider.ServiceCategoryId, provider.ProfessionalProfileId, other.ProfessionalProfileId);
        int proposalId = await SubmitProposalAsync(other.Token, serviceId);
        string url = $"{ProposalsUrl}/{proposalId}";

        using var response = await GetAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    public async Task GetProposal_Inexistente_Devolve404()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string url = $"{ProposalsUrl}/{int.MaxValue}";

        using var response = await GetAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F008-07")]
    public async Task GetProposal_ClienteDoServico_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var (client, serviceId, _) = await CreateDirectServiceAsync(provider.ServiceCategoryId, provider.ProfessionalProfileId);
        int proposalId = await SubmitProposalAsync(provider.Token, serviceId);

        using var response = await GetAsync(client.Token, $"{ProposalsUrl}/{proposalId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- E36 and E37: provider without a profile ----------

    [Theory]
    [InlineData(DirectRequestsUrl)]
    [InlineData(ProposalsUrl)]
    [Trait("Requisito", "RF-F004-01")]
    public async Task Get_PrestadorSemPerfil_Devolve403PerfilEmFalta(string baseUrl)
    {
        var (token, _, _) = await RegisterProviderAsync(_client);
        string url = $"{baseUrl}/1";

        using var response = await GetAsync(token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, MissingProfileType, url);
    }

    // ---------- Helpers ----------

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    // A service with direct requests to the given profiles (E20), at AddressAt10Km. Returns the client, the serviceId
    // and the directRequestId of each profile.
    private async Task<(TestClient Client, int ServiceId, Dictionary<int, int> Requests)> CreateDirectServiceAsync(
        int serviceCategoryId, params int[] professionalProfileIds)
    {
        var client = await RegisterClientAsync(_client);
        var body = DirectRequestBody(serviceCategoryId, professionalProfileIds);
        body["location"] = FakeGeocodingService.AddressAt10Km;
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, client.Token, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<JsonElement>();
        var requests = service.GetProperty("directRequests").EnumerateArray().ToDictionary(
            r => r.GetProperty("professionalProfileId").GetInt32(),
            r => r.GetProperty("directRequestId").GetInt32());
        return (client, service.GetProperty("serviceId").GetInt32(), requests);
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

    private async Task<int> SubmitProposalAsync(string token, int serviceId)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals", token,
            new { amount = 80.00m, conditions = "Inclui deslocação e material.", obs = "Disponível dia 9 às 18h30." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // RNF-06: only the name of the Client.
    private static void AssertHasNoClientContacts(JsonElement service)
    {
        foreach (string field in new[] { "clientId", "client", "email", "contactDetails", "address" })
        {
            Assert.False(service.TryGetProperty(field, out _), $"O serviço não pode ter o campo {field}.");
        }
    }
}