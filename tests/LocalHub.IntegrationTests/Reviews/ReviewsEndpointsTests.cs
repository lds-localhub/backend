using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Reviews;

// F012: E32 (review a service) and E15 (reviews of a provider), with the average rating and reviewCount of the profile
// (E13). The 401 cases are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class ReviewsEndpointsTests(LocalHubApiFactory factory)
{
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string InvalidTransitionType = "https://localhub.local/erros/transicao-invalida";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E32: review (criteria 1, 5 and 6) ----------

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    [Trait("Requisito", "RF-F012-02")]
    public async Task Review_CompletedService_Returns201AndUpdatesProfileAndService()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        string url = ReviewUrl(service.ServiceId);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, url, service.Client.Token,
            new { rating = 4, comment = "Rápida e cuidadosa." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var review = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(review.GetProperty("reviewId").GetInt32() > 0);
        Assert.Equal(4, review.GetProperty("rating").GetInt32());
        Assert.Equal("Rápida e cuidadosa.", review.GetProperty("comment").GetString());
        Assert.NotEqual(default, review.GetProperty("creationDate").GetDateTimeOffset());

        // The service stays COMPLETED and is marked as reviewed (E22).
        var stored = await GetJsonAsync(service.Client.Token, $"{ServicesUrl}/{service.ServiceId}");
        Assert.Equal("COMPLETED", stored.GetProperty("status").GetString());
        Assert.True(stored.GetProperty("reviewed").GetBoolean());

        // The review is of the provider of the accepted proposal (E13).
        var profile = await GetJsonAsync(service.Client.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(4.0m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, profile.GetProperty("reviewCount").GetInt32());
    }

    // ---------- E32: invalid fields (criterion 2) ----------

    // RF-F012-01, criteria 4 and 5: 0, 6 and 3.5 are refused, as a missing rating and a comment over 500 characters.
    public static TheoryData<string, string> InvalidBodies => new()
    {
        { """{ "rating": 0 }""", "rating" },
        { """{ "rating": 3.5 }""", "rating" },
        { """{ "comment": "Sem classificação." }""", "rating" },
        { $$"""{ "rating": 4, "comment": "{{new string('a', 501)}}" }""", "comment" },
        // A field that is not in the body of E32 (the JSON reader does not name it in errors).
        { """{ "rating": 4, "reviewed": true }""", "" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_InvalidData_Returns400WithoutReview(string body, string field)
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        string url = ReviewUrl(service.ServiceId);

        using var response = await PostRawAsync(service.Client.Token, url, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, BadRequestType, url);
        if (field.Length > 0)
        {
            Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
        }

        var stored = await GetJsonAsync(service.Client.Token, $"{ServicesUrl}/{service.ServiceId}");
        Assert.False(stored.GetProperty("reviewed").GetBoolean());
    }

    // ---------- E32: other actors (criterion 4) ----------

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_OtherClient_Returns403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        var other = await RegisterClientAsync(_client);
        string url = ReviewUrl(service.ServiceId);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, url, other.Token, new { rating = 4 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
        var stored = await GetJsonAsync(service.Client.Token, $"{ServicesUrl}/{service.ServiceId}");
        Assert.False(stored.GetProperty("reviewed").GetBoolean());
    }

    // ---------- E15 (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    [Trait("Requisito", "RF-F012-01")]
    public async Task ListReviews_Client_ReturnsReviewsNewestFirstPaginated()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var reviews = new List<(TestClient Client, int ReviewId)>();
        foreach (int rating in new[] { 3, 4, 5 })
        {
            var service = await CreateCompletedServiceAsync(provider);
            reviews.Add((service.Client, await ReviewAsync(service, rating, rating == 4 ? null : $"Nota {rating}.")));
        }

        // Another provider's review is not listed.
        var other = await CreateProviderWithProfileAsync(_client);
        await ReviewAsync(await CreateCompletedServiceAsync(other), 1);

        var page1 = await GetJsonAsync(reviews[0].Client.Token, $"{ReviewsUrl(provider.ProfessionalProfileId)}?pageSize=2");
        var page2 = await GetJsonAsync(reviews[0].Client.Token, $"{ReviewsUrl(provider.ProfessionalProfileId)}?page=2&pageSize=2");

        Assert.Equal(1, page1.GetProperty("page").GetInt32());
        Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, page1.GetProperty("total").GetInt32());
        var items = page1.GetProperty("items").EnumerateArray().Concat(page2.GetProperty("items").EnumerateArray()).ToList();
        Assert.Equal([reviews[2].ReviewId, reviews[1].ReviewId, reviews[0].ReviewId],
            items.Select(i => i.GetProperty("reviewId").GetInt32()));

        var newest = items[0];
        Assert.Equal(5, newest.GetProperty("rating").GetInt32());
        Assert.Equal("Nota 5.", newest.GetProperty("comment").GetString());
        Assert.Equal("Cliente Teste", newest.GetProperty("clientName").GetString());
        Assert.NotEqual(default, newest.GetProperty("creationDate").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("comment").ValueKind);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task ListReviews_ProviderOfOtherProfile_Returns403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderWithProfileAsync(_client);
        string url = ReviewsUrl(provider.ProfessionalProfileId);

        using var response = await GetAsync(other.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    // ---------- Helpers ----------

    private sealed record TestService(TestClient Client, int ServiceId);

    private static string ReviewUrl(int serviceId) => $"{ServicesUrl}/{serviceId}/review";

    private static string ProfileUrl(int professionalProfileId) => $"{ProfilesUrl}/{professionalProfileId}";

    private static string ReviewsUrl(int professionalProfileId) => $"{ProfilesUrl}/{professionalProfileId}/reviews";

    private Task<HttpResponseMessage> GetAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Get, url, token);

    private Task<HttpResponseMessage> PostAsync(string token, string url) =>
        SendWithTokenAsync(_client, HttpMethod.Post, url, token);

    private async Task<HttpResponseMessage> PostRawAsync(string token, string url, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> GetJsonAsync(string token, string url)
    {
        using var response = await GetAsync(token, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> ReviewAsync(TestService service, int rating, string? comment = null)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ReviewUrl(service.ServiceId),
            service.Client.Token, new { rating, comment });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reviewId").GetInt32();
    }

    private async Task<int> CreatePublicServiceAsync(string clientToken, int serviceCategoryId)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, clientToken, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    // A PUBLIC service of a new client, with the proposal of the provider accepted (ASSIGNED).
    private async Task<TestService> CreateAssignedServiceAsync(ProviderWithProfile provider)
    {
        var client = await RegisterClientAsync(_client);
        int serviceId = await CreatePublicServiceAsync(client.Token, provider.ServiceCategoryId);

        using var submit = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
            provider.Token, new { amount = 80.00m, conditions = "Material incluído." });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        int proposalId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();

        using var accept = await PostAsync(client.Token, $"/api/proposals/{proposalId}/accept");
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        return new TestService(client, serviceId);
    }

    // The same service, marked as completed by the client (E31).
    private async Task<TestService> CreateCompletedServiceAsync(ProviderWithProfile provider)
    {
        var service = await CreateAssignedServiceAsync(provider);
        using var complete = await PostAsync(service.Client.Token, $"{ServicesUrl}/{service.ServiceId}/complete");
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return service;
    }
}