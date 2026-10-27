using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Infrastructure;
using LocalHub.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;

namespace LocalHub.IntegrationTests.Auth;

// F001: E01 (register), E02 (login), E03 (logout), E04 (current user) and E40 (change password).
[Collection(ApiTestGroup.Name)]
[Trait("Category", "Integration")]
public class AuthEndpointsTests(LocalHubApiFactory factory)
{
    private const string PasswordUrl = "/api/users/me/password";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_ClienteValido_Devolve201ComLocationEContaAtiva()
    {
        string email = NewEmail();

        using var response = await RegisterAsync(_client, "Ana Costa", email, ValidPassword, "CLIENT");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/users/me", response.Headers.Location?.OriginalString);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("CLIENT", body.GetProperty("role").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_EmailJaUsadoComMaiusculas_Devolve409ComErroNoEmail()
    {
        string email = NewEmail();
        using var first = await RegisterAsync(_client, "Ana Costa", email, ValidPassword, "CLIENT");

        using var response = await RegisterAsync(_client, "Ana Costa", email.ToUpperInvariant(), ValidPassword, "PROVIDER");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await AssertProblemAsync(
            response, "https://tools.ietf.org/html/rfc9110#section-15.5.10", "/api/auth/register");
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_MesmoEmailEmSimultaneo_CriaApenasUmaConta()
    {
        string email = NewEmail();

        // The two requests run at the same time, so both can pass the e-mail check before either is saved;
        // the unique index on the e-mail must reject the second one with 409 instead of creating two accounts.
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            RegisterAsync(factory.CreateClient(), "Ana Costa", email, ValidPassword, "CLIENT")));

        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal(1, statusCodes.Count(s => s == HttpStatusCode.Created));
        Assert.All(statusCodes.Where(s => s != HttpStatusCode.Created),
            s => Assert.Equal(HttpStatusCode.Conflict, s));

        using var login = await LoginAsync(_client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_CamposInvalidosEPapelAdministrador_Devolve400ComErroPorCampo()
    {
        using var response = await RegisterAsync(_client, "", "invalido", "curta", "ADMIN");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(
            response, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "/api/auth/register");
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("role", out _));
    }

    [Theory]
    [InlineData("abcdefgh")] // 8 characters, no digit
    [InlineData("1234567")] // no letter and only 7 characters
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_PalavraPasseFraca_Devolve400ComErroNaPalavraPasseENaoCriaConta(string password)
    {
        string email = NewEmail();

        using var response = await RegisterAsync(_client, "Ana Costa", email, password, "CLIENT");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.Equal(0, await CountUsersAsync(email));
    }

    [Fact]
    [Trait("Requisito", "RNF-01")]
    public async Task Register_DuasContasComMesmaPalavraPasse_GuardaResumosDiferentesEAmbasIniciamSessao()
    {
        string firstEmail = NewEmail();
        string secondEmail = NewEmail();
        using var first = await RegisterAsync(_client, "Ana Costa", firstEmail, ValidPassword, "CLIENT");
        using var second = await RegisterAsync(_client, "Rui Costa", secondEmail, ValidPassword, "PROVIDER");

        string firstHash = await GetPasswordHashAsync(firstEmail);
        string secondHash = await GetPasswordHashAsync(secondEmail);
        using var firstLogin = await LoginAsync(_client, firstEmail, ValidPassword);
        using var secondLogin = await LoginAsync(_client, secondEmail, ValidPassword);

        Assert.NotEqual(firstHash, secondHash);
        Assert.DoesNotContain(ValidPassword, firstHash, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-01")]
    public async Task Register_PapelDesconhecido_Devolve400ComErroNoPapel()
    {
        using var response = await RegisterAsync(_client, "Ana Costa", NewEmail(), ValidPassword, "XYZ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(
            response, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "/api/auth/register");
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("role", out _));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-02")]
    [Trait("Requisito", "RF-F001-05")]
    public async Task Login_AdministradorDoConjuntoInicial_Devolve200ComTokenESessaoDeOitoHoras()
    {
        using var response = await LoginAsync(_client, AdminEmail, LocalHubApiFactory.SeedPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("ADMIN", body.GetProperty("user").GetProperty("role").GetString());

        var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expiresAt - DateTimeOffset.UtcNow, TimeSpan.FromHours(7.9), TimeSpan.FromHours(8));
    }

    [Fact]
    [Trait("Requisito", "RF-F001-02")]
    public async Task Login_EmailComMaiusculas_Devolve200()
    {
        using var response = await LoginAsync(_client, AdminEmail.ToUpperInvariant(), LocalHubApiFactory.SeedPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-02")]
    public async Task Login_PalavraPasseErradaOuEmailDesconhecido_Devolve401ComMesmaMensagem()
    {
        using var wrongPassword = await LoginAsync(_client, AdminEmail, "errada123");
        using var unknownEmail = await LoginAsync(_client, NewEmail(), "errada123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var first = await AssertProblemAsync(
            wrongPassword, "https://localhub.local/erros/credenciais-invalidas", "/api/auth/login");
        var second = await AssertProblemAsync(
            unknownEmail, "https://localhub.local/erros/credenciais-invalidas", "/api/auth/login");
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-F001-02")]
    public async Task Login_CamposEmFalta_Devolve400()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-03")]
    public async Task GetMe_ComToken_DevolveUtilizadorAutenticado()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email);

        using var response = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/users/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("PROVIDER", body.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("latitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("professionalProfileId").ValueKind);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-04")]
    public async Task Logout_SessaoValida_Devolve204()
    {
        string token = await RegisterAndLoginAsync(_client, NewEmail());

        using var response = await SendWithTokenAsync(_client, HttpMethod.Post, "/api/auth/logout", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-07")]
    public async Task ChangePassword_PalavraPasseAtualErrada_Devolve400ComErroNoCampo()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email, "CLIENT");

        using var response = await ChangePasswordAsync(token, "errada2026", "jardim2027");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, PasswordUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty("currentPassword", out _));
        using var login = await LoginAsync(_client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData("abcdefgh")]
    [InlineData("1234567")]
    [Trait("Requisito", "RF-F001-07")]
    public async Task ChangePassword_NovaPalavraPasseFraca_Devolve400ComErroNoCampo(string newPassword)
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email, "PROVIDER");

        using var response = await ChangePasswordAsync(token, ValidPassword, newPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await AssertProblemAsync(response, BadRequestType, PasswordUrl);
        Assert.True(body.GetProperty("errors").TryGetProperty("newPassword", out _));
        using var login = await LoginAsync(_client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-07")]
    [Trait("Requisito", "RNF-01")]
    public async Task ChangePassword_DadosValidos_NovaPalavraPasseSubstituiAnterior()
    {
        string email = NewEmail();
        string token = await RegisterAndLoginAsync(_client, email, "CLIENT");
        string oldHash = await GetPasswordHashAsync(email);

        using var response = await ChangePasswordAsync(token, ValidPassword, "jardim2027");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var newLogin = await LoginAsync(_client, email, "jardim2027");
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        using var oldLogin = await LoginAsync(_client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        string newHash = await GetPasswordHashAsync(email);
        Assert.NotEqual(oldHash, newHash);
        Assert.NotEqual("jardim2027", newHash);
    }

    [Fact]
    [Trait("Requisito", "RF-F001-07")]
    public async Task ChangePassword_DadosValidos_RevogaOutrasSessoesEMantemAtual()
    {
        string email = NewEmail();
        string tokenA = await RegisterAndLoginAsync(_client, email, "CLIENT");
        string tokenB = await LoginAndGetTokenAsync(_client, email, ValidPassword);

        using var response = await ChangePasswordAsync(tokenA, ValidPassword, "jardim2027");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var meB = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/users/me", tokenB);
        Assert.Equal(HttpStatusCode.Unauthorized, meB.StatusCode);
        using var meA = await SendWithTokenAsync(_client, HttpMethod.Get, "/api/users/me", tokenA);
        Assert.Equal(HttpStatusCode.OK, meA.StatusCode);
    }

    // E40 with a body; SendWithTokenAsync only sends requests without a body.
    private async Task<HttpResponseMessage> ChangePasswordAsync(string token, string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, PasswordUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { currentPassword, newPassword });
        return await _client.SendAsync(request);
    }

    private async Task<int> CountUsersAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.CountAsync(u => u.Email == email);
    }

    private async Task<string> GetPasswordHashAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync();
    }
}