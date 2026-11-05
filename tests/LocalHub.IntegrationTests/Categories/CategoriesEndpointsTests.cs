using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.IntegrationTests.Fixtures;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;
using static LocalHub.IntegrationTests.Fixtures.ServiceRequests;

namespace LocalHub.IntegrationTests.Categories;

[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class CategoriesEndpointsTests(LocalHubApiFactory factory)
{
    private const string CategoriesUrl = "/api/categories";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F003-04")]
    public async Task List_WithoutSession_ReturnsOnlyActiveCategoriesSortedByName()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string tag = NewTag();
        // Created in reverse order, so the order of the list comes from the name.
        var second = await CreateAsync(adminToken, $"Teste Lista B {tag}");
        var first = await CreateAsync(adminToken, $"Teste Lista A {tag}");
        var inactive = await CreateAsync(adminToken, $"Teste Lista C {tag}");
        await DeactivateAsync(adminToken, inactive.Id);

        var categories = await ListAsync(token: null, includeInactive: false);

        Assert.All(categories, c => Assert.Equal("ACTIVE", c.GetProperty("status").GetString()));
        var ids = categories.Select(c => c.GetProperty("serviceCategoryId").GetInt32()).ToList();
        Assert.Contains(first.Id, ids);
        Assert.Contains(second.Id, ids);
        Assert.DoesNotContain(inactive.Id, ids);
        Assert.True(ids.IndexOf(first.Id) < ids.IndexOf(second.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public async Task Create_NewName_Returns201WithLocationAndActiveCategoryInList()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string name = $"Teste Criar {NewTag()}";

        using var response = await SendJsonAsync(HttpMethod.Post, CategoriesUrl, adminToken, new { name = $"  {name}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        int id = body.GetProperty("serviceCategoryId").GetInt32();
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
        Assert.Null(response.Headers.Location);
        Assert.Equal("ACTIVE", StatusOf(await ListAsync(adminToken, includeInactive: true), id));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public async Task Create_ExistingNameWithDifferentCase_Returns409WithNameError()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string name = $"Teste Canalização {NewTag()}";
        await CreateAsync(adminToken, name);

        using var response = await SendJsonAsync(
            HttpMethod.Post, CategoriesUrl, adminToken, new { name = name.ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await AssertProblemAsync(response, ConflictType, CategoriesUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-02")]
    public async Task Rename_ValidUnusedName_Returns200AndNewNameInList()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var category = await CreateAsync(adminToken, $"Teste Antigo {NewTag()}");
        string newName = $"Teste Novo {NewTag()}";

        using var response = await SendJsonAsync(
            HttpMethod.Put, $"{CategoriesUrl}/{category.Id}", adminToken, new { name = newName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(category.Id, body.GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal(newName, body.GetProperty("name").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
        var listed = (await ListAsync(adminToken, includeInactive: true))
            .Single(c => c.GetProperty("serviceCategoryId").GetInt32() == category.Id);
        Assert.Equal(newName, listed.GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    [Trait("Requisito", "RF-F003-04")]
    public async Task Deactivate_UnusedCategory_Returns200AndLeavesPublicList()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var category = await CreateAsync(adminToken, $"Teste Desativar {NewTag()}");
        string url = $"{CategoriesUrl}/{category.Id}/deactivate";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(category.Id, body.GetProperty("serviceCategoryId").GetInt32());
        Assert.Equal("INACTIVE", body.GetProperty("status").GetString());
        var publicIds = (await ListAsync(token: null, includeInactive: false))
            .Select(c => c.GetProperty("serviceCategoryId").GetInt32());
        Assert.DoesNotContain(category.Id, publicIds);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public async Task Deactivate_CategoryWithActiveProvidersOrOngoingServices_Returns409WithReasonAndStaysActive()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var provider = await CreateProviderWithProfileAsync(_client);
        var client = await RegisterClientAsync(_client);
        using var created = await ProfileRequests.SendJsonAsync(
            _client, HttpMethod.Post, ServicesUrl, client.Token, PublicBody(provider.ServiceCategoryId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string url = $"{CategoriesUrl}/{provider.ServiceCategoryId}/deactivate";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await AssertProblemAsync(response, ConflictType, url);
        Assert.Equal("Categoria em utilização", body.GetProperty("title").GetString());
        Assert.Equal(
            "A categoria está associada a 1 Prestador ativo e a 1 serviço em curso.",
            body.GetProperty("detail").GetString());
        Assert.Equal("ACTIVE", StatusOf(await ListAsync(adminToken, includeInactive: true), provider.ServiceCategoryId));
    }

    private static string NewTag() => Guid.NewGuid().ToString("N");

    private async Task<(int Id, string Name)> CreateAsync(string adminToken, string name)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, CategoriesUrl, adminToken, new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("serviceCategoryId").GetInt32(), body.GetProperty("name").GetString()!);
    }

    private async Task DeactivateAsync(string adminToken, int id)
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Post, $"{CategoriesUrl}/{id}/deactivate", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<JsonElement>> ListAsync(string? token, bool includeInactive)
    {
        string url = includeInactive ? $"{CategoriesUrl}?includeInactive=true" : CategoriesUrl;
        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("page", out _));
        var items = body.GetProperty("items").EnumerateArray().Select(c => c.Clone()).ToList();
        Assert.Equal(items.Count, body.GetProperty("total").GetInt32());
        return items;
    }

    private static string? StatusOf(List<JsonElement> categories, int id) =>
        categories.Single(c => c.GetProperty("serviceCategoryId").GetInt32() == id).GetProperty("status").GetString();

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}