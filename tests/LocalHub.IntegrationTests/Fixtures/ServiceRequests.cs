using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;

namespace LocalHub.IntegrationTests.Fixtures;

public record TestClient(string Token, int UserId);

public static class ServiceRequests
{
    public const string ServicesUrl = "/api/services";
    public const string IsoDate = "yyyy-MM-dd";

    // E20 validates the dates in UTC.
    public static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    public static async Task<TestClient> RegisterClientAsync(HttpClient client, bool withLocation = true)
    {
        string email = NewEmail();
        using var register = await RegisterAsync(client, "Cliente Teste", email, ValidPassword, "CLIENT");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        string token = await LoginAndGetTokenAsync(client, email, ValidPassword);

        if (withLocation)
        {
            using var location = await SendJsonAsync(client, HttpMethod.Put, "/api/users/me/location", token,
                new { address = FakeGeocodingService.KnownAddress });
            Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        }

        return new TestClient(token, body.GetProperty("userId").GetInt32());
    }

    public static Dictionary<string, object?> DirectRequestBody(int serviceCategoryId, params int[] professionalProfileIds) =>
        new()
        {
            ["proposalModel"] = "DIRECT_REQUEST",
            ["serviceCategoryId"] = serviceCategoryId,
            ["title"] = "Reparar fuga na torneira",
            ["description"] = "Torneira da casa de banho com fuga de água.",
            ["requestedStartDate"] = Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            ["requestedEndDate"] = Today.AddDays(3).ToString(IsoDate, CultureInfo.InvariantCulture),
            ["location"] = FakeGeocodingService.KnownAddress,
            ["professionalProfileIds"] = professionalProfileIds,
        };

    public static Dictionary<string, object?> PublicBody(int serviceCategoryId) => new()
    {
        ["proposalModel"] = "PUBLIC",
        ["serviceCategoryId"] = serviceCategoryId,
        ["title"] = "Substituir torneira",
        ["description"] = "Substituir a torneira da cozinha que pinga.",
        ["requestedStartDate"] = Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
        ["location"] = FakeGeocodingService.KnownAddress,
    };
}