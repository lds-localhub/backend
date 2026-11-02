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
    private const string ReviewExistsType = "https://localhub.local/erros/avaliacao-existente";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E32: review (criteria 1, 5 and 6) ----------

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    [Trait("Requisito", "RF-F012-02")]
    public async Task Review_ServicoRealizado_Devolve201EAtualizaOPerfilEOServico()
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
        var asProvider = await GetJsonAsync(provider.Token, $"{ServicesUrl}/{service.ServiceId}");
        Assert.True(asProvider.GetProperty("reviewed").GetBoolean());

        // The review is of the provider of the accepted proposal (E13).
        var profile = await GetJsonAsync(service.Client.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(4.0m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, profile.GetProperty("reviewCount").GetInt32());
    }

    // RF-F012-02, criteria 1 and 2: 5, 4 and 4 give 4.3; a new review of 1 gives 3.5.
    [Fact]
    [Trait("Requisito", "RF-F012-02")]
    public async Task Review_VariasAvaliacoes_RecalculaAMediaComUmaCasaDecimal()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        TestClient? lastClient = null;
        foreach (int rating in new[] { 5, 4, 4 })
        {
            var service = await CreateCompletedServiceAsync(provider);
            await ReviewAsync(service, rating);
            lastClient = service.Client;
        }

        var profile = await GetJsonAsync(lastClient!.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(4.3m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(3, profile.GetProperty("reviewCount").GetInt32());

        var fourth = await CreateCompletedServiceAsync(provider);
        await ReviewAsync(fourth, 1);

        var mine = await GetJsonAsync(provider.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(3.5m, mine.GetProperty("averageRating").GetDecimal());
        Assert.Equal(4, mine.GetProperty("reviewCount").GetInt32());
    }

    // RF-F012-02, criterion 3: a provider without reviews has no average.
    [Fact]
    [Trait("Requisito", "RF-F012-02")]
    public async Task Perfil_SemAvaliacoes_DevolveMediaNulaEContagemZero()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        var profile = await GetJsonAsync(provider.Token, ProfileUrl(provider.ProfessionalProfileId));

        Assert.Equal(JsonValueKind.Null, profile.GetProperty("averageRating").ValueKind);
        Assert.Equal(0, profile.GetProperty("reviewCount").GetInt32());
    }

    // Criterion 7 of RF-F012-01 and API documentation (E32): without a comment, or blank, the comment is null.
    [Theory]
    [Trait("Requisito", "RF-F012-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Review_SemComentario_GuardaComentarioNulo(string? comment)
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ReviewUrl(service.ServiceId),
            service.Client.Token, new Dictionary<string, object?> { ["rating"] = 5, ["comment"] = comment });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var review = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, review.GetProperty("comment").ValueKind);
    }

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_ComentarioCom500Caracteres_Devolve201()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, ReviewUrl(service.ServiceId),
            service.Client.Token, new { rating = 3, comment = new string('a', 500) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- E32: invalid fields (criterion 2) ----------

    // RF-F012-01, criteria 4 and 5: 0, 6 and 3.5 are refused, as a missing rating and a comment over 500 characters.
    public static TheoryData<string, string> InvalidBodies => new()
    {
        { """{ "rating": 0 }""", "rating" },
        { """{ "rating": 6 }""", "rating" },
        { """{ "rating": 3.5 }""", "rating" },
        { """{ "comment": "Sem classificação." }""", "rating" },
        { $$"""{ "rating": 4, "comment": "{{new string('a', 501)}}" }""", "comment" },
        // A field that is not in the body of E32 (the JSON reader does not name it in errors).
        { """{ "rating": 4, "reviewed": true }""", "" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_DadosInvalidos_Devolve400SemAvaliar(string body, string field)
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

    // ---------- E32: state and single review (criterion 3) ----------

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    [Trait("Requisito", "RF-F007-07")]
    public async Task Review_ServicoAtribuido_Devolve409TransicaoInvalida()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateAssignedServiceAsync(provider);
        string url = ReviewUrl(service.ServiceId);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, url, service.Client.Token, new { rating = 4 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, InvalidTransitionType, url);
        var profile = await GetJsonAsync(service.Client.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(0, profile.GetProperty("reviewCount").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    [Trait("Requisito", "RF-F008-06")]
    public async Task Review_TrabalhoAbertoECancelado_Devolve409TransicaoInvalida()
    {
        var client = await RegisterClientAsync(_client);
        var provider = await CreateProviderWithProfileAsync(_client);
        int open = await PublishJobAsync(client.Token, provider.ServiceCategoryId);
        int cancelled = await PublishJobAsync(client.Token, provider.ServiceCategoryId);
        using (var cancel = await PostAsync(client.Token, $"{ServicesUrl}/{cancelled}/cancel"))
        {
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        }

        foreach (int serviceId in new[] { open, cancelled })
        {
            string url = ReviewUrl(serviceId);
            using var response = await SendJsonAsync(_client, HttpMethod.Post, url, client.Token, new { rating = 4 });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AssertProblemAsync(response, InvalidTransitionType, url);
        }
    }

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_SegundaAvaliacao_Devolve409AvaliacaoExistenteEMantemAPrimeira()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        await ReviewAsync(service, 5);
        string url = ReviewUrl(service.ServiceId);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, url, service.Client.Token, new { rating = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ReviewExistsType, url);
        var profile = await GetJsonAsync(service.Client.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(5.0m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, profile.GetProperty("reviewCount").GetInt32());
    }

    // Two reviews of the same service at the same time: only one is saved (UNIQUE of serviceId and lock of the profile).
    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_DuasAvaliacoesEmSimultaneo_SoUmaERegistada()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        string url = ReviewUrl(service.ServiceId);

        var responses = await Task.WhenAll(
            SendJsonAsync(_client, HttpMethod.Post, url, service.Client.Token, new { rating = 5 }),
            SendJsonAsync(_client, HttpMethod.Post, url, service.Client.Token, new { rating = 1 }));

        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var profile = await GetJsonAsync(service.Client.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(1, profile.GetProperty("reviewCount").GetInt32());
    }

    // Criterion 6 of RF-F012-01: two reviews of the same provider at the same time are both counted. The test holds the
    // lock of the profile until both requests have read it and are waiting, so the average of the second one (4.0) is
    // the same that it read at the start and must still replace the average saved by the first one.
    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    [Trait("Requisito", "RF-F012-02")]
    public async Task Review_DuasAvaliacoesDoMesmoPrestadorEmSimultaneo_ContaAsDuasNaMedia()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        await ReviewAsync(await CreateCompletedServiceAsync(provider), 4);
        var first = await CreateCompletedServiceAsync(provider);
        var second = await CreateCompletedServiceAsync(provider);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM \"ProfessionalProfile\" WHERE \"professionalProfileId\" = {provider.ProfessionalProfileId} FOR UPDATE");

        var requests = Task.WhenAll(
            SendJsonAsync(_client, HttpMethod.Post, ReviewUrl(first.ServiceId), first.Client.Token, new { rating = 5 }),
            SendJsonAsync(_client, HttpMethod.Post, ReviewUrl(second.ServiceId), second.Client.Token, new { rating = 3 }));
        await WaitForRequestsBlockedOnProfileAsync(expected: 2);
        await transaction.CommitAsync();

        var responses = await requests;
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var profile = await GetJsonAsync(first.Client.Token, ProfileUrl(provider.ProfessionalProfileId));
        Assert.Equal(4.0m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(3, profile.GetProperty("reviewCount").GetInt32());
    }

    // ---------- E32: other actors (criterion 4) ----------

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_OutroCliente_Devolve403()
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

    // Rule 7 of RF-F012-01: the review is one-way; the provider cannot review (nor the administrator).
    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_PrestadorContratadoEAdministrador_Devolvem403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        string adminToken = await LoginAsAdminAsync(_client);
        string url = ReviewUrl(service.ServiceId);

        using var asProvider = await SendJsonAsync(_client, HttpMethod.Post, url, provider.Token, new { rating = 4 });
        using var asAdmin = await SendJsonAsync(_client, HttpMethod.Post, url, adminToken, new { rating = 4 });

        Assert.Equal(HttpStatusCode.Forbidden, asProvider.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public async Task Review_ServicoInexistente_Devolve404()
    {
        var client = await RegisterClientAsync(_client);
        string url = ReviewUrl(int.MaxValue);

        using var response = await SendJsonAsync(_client, HttpMethod.Post, url, client.Token, new { rating = 4 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    // ---------- E15 (criterion 7) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    [Trait("Requisito", "RF-F012-01")]
    public async Task ListReviews_Cliente_DevolveAsAvaliacoesDaMaisRecenteParaAMaisAntigaPaginadas()
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
    public async Task ListReviews_PrestadorDoProprioPerfil_Devolve200()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        await ReviewAsync(await CreateCompletedServiceAsync(provider), 4);

        var list = await GetJsonAsync(provider.Token, ReviewsUrl(provider.ProfessionalProfileId));

        Assert.Equal(1, list.GetProperty("total").GetInt32());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task ListReviews_SemAvaliacoes_DevolveListaVazia()
    {
        var provider = await CreateProviderWithProfileAsync(_client);

        var list = await GetJsonAsync(provider.Token, ReviewsUrl(provider.ProfessionalProfileId));

        Assert.Equal(0, list.GetProperty("total").GetInt32());
        Assert.Empty(list.GetProperty("items").EnumerateArray());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task ListReviews_PrestadorDeOutroPerfil_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var other = await CreateProviderWithProfileAsync(_client);
        string url = ReviewsUrl(provider.ProfessionalProfileId);

        using var response = await GetAsync(other.Token, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task ListReviews_Administrador_Devolve403()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await GetAsync(adminToken, ReviewsUrl(provider.ProfessionalProfileId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    public async Task ListReviews_PerfilInexistente_Devolve404ParaOClienteEParaOPrestador()
    {
        var client = await RegisterClientAsync(_client);
        var provider = await CreateProviderWithProfileAsync(_client);
        string url = ReviewsUrl(int.MaxValue);

        using var asClient = await GetAsync(client.Token, url);
        using var asProvider = await GetAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.NotFound, asClient.StatusCode);
        await AssertProblemAsync(asClient, NotFoundType, url);
        Assert.Equal(HttpStatusCode.NotFound, asProvider.StatusCode);
    }

    // RF-F004-03, rule 2 (as in E13): the reviews of a provider with a deactivated account are not shown to the client.
    [Fact]
    [Trait("Requisito", "RF-F004-03")]
    [Trait("Requisito", "RF-F002-02")]
    public async Task ListReviews_PrestadorDesativado_Devolve404ParaOCliente()
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        var service = await CreateCompletedServiceAsync(provider);
        await ReviewAsync(service, 4);
        string adminToken = await LoginAsAdminAsync(_client);
        using (var deactivate = await PostAsync(adminToken, $"/api/accounts/{provider.UserId}/deactivate"))
        {
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        }

        using var response = await GetAsync(service.Client.Token, ReviewsUrl(provider.ProfessionalProfileId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [Trait("Requisito", "RF-F004-03")]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=51", "pageSize")]
    public async Task ListReviews_PaginacaoInvalida_Devolve400(string query, string field)
    {
        var provider = await CreateProviderWithProfileAsync(_client);
        string url = $"{ReviewsUrl(provider.ProfessionalProfileId)}?{query}";

        using var response = await GetAsync(provider.Token, url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
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

    // Uses its own connection, without a transaction, because PostgreSQL keeps the same pg_stat_activity inside one.
    private async Task WaitForRequestsBlockedOnProfileAsync(int expected)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        int blocked = 0;
        for (int attempt = 0; attempt < 200 && blocked < expected; attempt++)
        {
            blocked = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%FOR UPDATE%'
                """).SingleAsync();
            if (blocked < expected)
            {
                await Task.Delay(50);
            }
        }

        Assert.Equal(expected, blocked);
    }

    private async Task<int> ReviewAsync(TestService service, int rating, string? comment = null)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ReviewUrl(service.ServiceId),
            service.Client.Token, new { rating, comment });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reviewId").GetInt32();
    }

    private async Task<int> PublishJobAsync(string clientToken, int serviceCategoryId)
    {
        using var response = await SendJsonAsync(_client, HttpMethod.Post, ServicesUrl, clientToken, PublicBody(serviceCategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceId").GetInt32();
    }

    // A published job of a new client, with the proposal of the provider accepted (ASSIGNED).
    private async Task<TestService> CreateAssignedServiceAsync(ProviderWithProfile provider)
    {
        var client = await RegisterClientAsync(_client);
        int serviceId = await PublishJobAsync(client.Token, provider.ServiceCategoryId);

        using var submit = await SendJsonAsync(_client, HttpMethod.Post, $"{ServicesUrl}/{serviceId}/proposals",
            provider.Token, new { amount = 80.00m, conditions = "Material incluído." });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        int proposalId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proposalId").GetInt32();

        using var accept = await PostAsync(client.Token, $"{ServicesUrl}/{serviceId}/proposals/{proposalId}/accept");
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