using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LocalHub.Domain.Services;

using static LocalHub.IntegrationTests.Fixtures.AuthRequests;
using static LocalHub.IntegrationTests.Fixtures.ProfileRequests;

namespace LocalHub.IntegrationTests.Fixtures;

// A client created through the API (E01, E02 and, when asked, E16 with the address known by FakeGeocodingService).
public record TestClient(string Token, int UserId);

// Requests to the service endpoints (F007 to F011) that several test classes use.
public static class ServiceRequests
{
    public const string ServicesUrl = "/api/services";
    public const string IsoDate = "yyyy-MM-dd";

    // The date in Portugal, the one used by E20 to validate the requested dates.
    public static DateOnly Today => Service.TodayInPortugal(DateTimeOffset.UtcNow);

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

    // A valid body of E20 with direct requests; without location, so the client's registered location is used.
    public static Dictionary<string, object?> DirectRequestBody(int serviceCategoryId, params int[] professionalProfileIds) =>
        new()
        {
            ["proposalModel"] = "DIRECT_REQUEST",
            ["serviceCategoryId"] = serviceCategoryId,
            ["description"] = "Torneira da casa de banho com fuga de água.",
            ["requestedStartDate"] = Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
            ["requestedEndDate"] = Today.AddDays(3).ToString(IsoDate, CultureInfo.InvariantCulture),
            ["professionalProfileIds"] = professionalProfileIds,
        };

    // A valid body of E20 for a published job, with a single date.
    public static Dictionary<string, object?> PublicBody(int serviceCategoryId) => new()
    {
        ["proposalModel"] = "PUBLIC",
        ["serviceCategoryId"] = serviceCategoryId,
        ["description"] = "Substituir a torneira da cozinha que pinga.",
        ["requestedStartDate"] = Today.AddDays(1).ToString(IsoDate, CultureInfo.InvariantCulture),
    };
}