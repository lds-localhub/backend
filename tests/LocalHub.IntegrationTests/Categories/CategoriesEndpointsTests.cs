using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure.Categories;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Categories;

// F003: E08 (list categories), E09 (create), E10 (rename) and E11 (deactivate).
// The 401 and 403 cases of E09 to E11 are in AccessControlTests.
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class CategoriesEndpointsTests(LocalHubApiFactory factory)
{
    private const string CategoriesUrl = "/api/categories";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string UnauthorizedType = "https://tools.ietf.org/html/rfc9110#section-15.5.2";
    private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- E08 ----------

    [Fact]
    [Trait("Requisito", "RF-F003-04")]
    public async Task List_SemSessao_DevolveApenasCategoriasAtivasOrdenadasPorNome()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string tag = NewTag();
        // Created in reverse order, so that the order of the list comes from the name.
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
    [Trait("Requisito", "RF-F003-04")]
    public async Task List_ClienteComSessaoSemIncludeInactive_DevolveApenasCategoriasAtivas()
    {
        string clientToken = await RegisterAndLoginAsync(_client, NewEmail(), "CLIENT");

        var categories = await ListAsync(clientToken, includeInactive: false);

        Assert.All(categories, c => Assert.Equal("ACTIVE", c.GetProperty("status").GetString()));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-04")]
    public async Task List_AdministradorComIncludeInactive_DevolveAtivasEDesativadasComOEstado()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var active = await CreateAsync(adminToken, $"Teste Estado A {NewTag()}");
        var inactive = await CreateAsync(adminToken, $"Teste Estado B {NewTag()}");
        await DeactivateAsync(adminToken, inactive.Id);

        var categories = await ListAsync(adminToken, includeInactive: true);

        Assert.Equal("ACTIVE", StatusOf(categories, active.Id));
        Assert.Equal("INACTIVE", StatusOf(categories, inactive.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-04")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task List_IncludeInactiveSemSessao_Devolve401()
    {
        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, $"{CategoriesUrl}?includeInactive=true", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, UnauthorizedType, CategoriesUrl);
    }

    [Theory]
    [InlineData("CLIENT")]
    [InlineData("PROVIDER")]
    [Trait("Requisito", "RF-F003-04")]
    [Trait("Requisito", "RF-F001-03")]
    public async Task List_IncludeInactiveComPapelNaoAdministrador_Devolve403(string role)
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail(), role);

        using var response = await SendWithTokenAsync(
            _client, HttpMethod.Get, $"{CategoriesUrl}?includeInactive=true", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, ForbiddenType, CategoriesUrl);
    }

    // ---------- E09 ----------

    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public async Task Create_NomeNovo_Devolve201ComLocationECategoriaAtivaNaLista()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string name = $"Teste Criar {NewTag()}";

        using var response = await SendJsonAsync(HttpMethod.Post, CategoriesUrl, adminToken, new { name = $"  {name}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        int id = body.GetProperty("serviceCategoryId").GetInt32();
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
        Assert.Equal($"{CategoriesUrl}/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("ACTIVE", StatusOf(await ListAsync(adminToken, includeInactive: true), id));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public async Task Create_NomeExistenteComOutrasMaiusculas_Devolve409ComErroNoNome()
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 61 characters
    [Trait("Requisito", "RF-F003-01")]
    public async Task Create_NomeVazioOuCom61Caracteres_Devolve400ComErroNoNome(string name)
    {
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendJsonAsync(HttpMethod.Post, CategoriesUrl, adminToken, new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, CategoriesUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public async Task Create_SemNome_Devolve400ComErroNoNome()
    {
        string adminToken = await LoginAsAdminAsync(_client);

        using var response = await SendJsonAsync(HttpMethod.Post, CategoriesUrl, adminToken, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, CategoriesUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    // ---------- E10 ----------

    [Fact]
    [Trait("Requisito", "RF-F003-02")]
    public async Task Rename_NomeValidoENaoUtilizado_Devolve200ENovoNomeNaLista()
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
    [Trait("Requisito", "RF-F003-02")]
    public async Task Rename_ProprioNomeComOutrasMaiusculas_Devolve200()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string name = $"Teste Maiúsculas {NewTag()}";
        var category = await CreateAsync(adminToken, name);

        using var response = await SendJsonAsync(
            HttpMethod.Put, $"{CategoriesUrl}/{category.Id}", adminToken, new { name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-02")]
    public async Task Rename_NomeDeOutraCategoria_Devolve409ComErroNoNomeEMantemONome()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var other = await CreateAsync(adminToken, $"Teste Outra {NewTag()}");
        string originalName = $"Teste Original {NewTag()}";
        var category = await CreateAsync(adminToken, originalName);
        string url = $"{CategoriesUrl}/{category.Id}";

        using var response = await SendJsonAsync(HttpMethod.Put, url, adminToken, new { name = other.Name });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await AssertProblemAsync(response, ConflictType, url);
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
        var listed = (await ListAsync(adminToken, includeInactive: true))
            .Single(c => c.GetProperty("serviceCategoryId").GetInt32() == category.Id);
        Assert.Equal(originalName, listed.GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F003-02")]
    public async Task Rename_NomeVazio_Devolve400ComErroNoNome()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var category = await CreateAsync(adminToken, $"Teste Vazio {NewTag()}");
        string url = $"{CategoriesUrl}/{category.Id}";

        using var response = await SendJsonAsync(HttpMethod.Put, url, adminToken, new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, url);
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-02")]
    public async Task Rename_CategoriaInexistente_Devolve404()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string url = $"{CategoriesUrl}/{int.MaxValue}";

        using var response = await SendJsonAsync(HttpMethod.Put, url, adminToken, new { name = $"Teste {NewTag()}" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    // ---------- E11 ----------

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    [Trait("Requisito", "RF-F003-04")]
    public async Task Deactivate_CategoriaSemUtilizacao_Devolve200EDeixaDeAparecerNaListaPublica()
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
    public async Task Deactivate_CategoriaJaDesativada_Devolve409()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var category = await CreateAsync(adminToken, $"Teste Já Desativada {NewTag()}");
        await DeactivateAsync(adminToken, category.Id);
        string url = $"{CategoriesUrl}/{category.Id}/deactivate";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, ConflictType, url);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public async Task Deactivate_CategoriaInexistente_Devolve404()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        string url = $"{CategoriesUrl}/{int.MaxValue}/deactivate";

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemAsync(response, NotFoundType, url);
    }

    // The professional profiles and the services are not implemented yet (CategoryUsage), so the use of the category
    // is simulated here: the API is started with a test ICategoryUsage that reports 3 active providers and 1 service.
    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public async Task Deactivate_CategoriaComPrestadoresAtivosOuServicosEmCurso_Devolve409ComMotivoEMantemAtiva()
    {
        string adminToken = await LoginAsAdminAsync(_client);
        var category = await CreateAsync(adminToken, $"Teste Em Utilização {NewTag()}");
        string url = $"{CategoriesUrl}/{category.Id}/deactivate";
        using var factoryWithUsage = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ICategoryUsage>(_ => new FixedCategoryUsage(new CategoryUsageCount(3, 1)))));
        using var clientWithUsage = factoryWithUsage.CreateClient();

        using var response = await SendWithTokenAsync(clientWithUsage, HttpMethod.Post, url, adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await AssertProblemAsync(response, ConflictType, url);
        Assert.Equal("Categoria em utilização", body.GetProperty("title").GetString());
        Assert.Equal(
            "A categoria está associada a 3 Prestadores ativos e a 1 serviço em curso.",
            body.GetProperty("detail").GetString());
        Assert.Equal("ACTIVE", StatusOf(await ListAsync(adminToken, includeInactive: true), category.Id));
    }

    // ---------- helpers ----------

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
        return body.EnumerateArray().Select(c => c.Clone()).ToList();
    }

    private static string? StatusOf(List<JsonElement> categories, int id) =>
        categories.Single(c => c.GetProperty("serviceCategoryId").GetInt32() == id).GetProperty("status").GetString();

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed class FixedCategoryUsage(CategoryUsageCount usage) : ICategoryUsage
    {
        public Task<CategoryUsageCount> CountAsync(int serviceCategoryId, CancellationToken cancellationToken) =>
            Task.FromResult(usage);
    }
}