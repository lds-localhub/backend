using System.Text.Json.Serialization;

namespace LocalHub.Application.Accounts.Dtos;

public record AccountAlertResponse(
    AccountAlertType Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AverageRating = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ReviewCount = null)
{
    public const int MinAssignedServices = 3;
    public const int MinReviews = 3;
    public const decimal MaxLowAverageRating = 2.0m;

    public static AccountAlertResponse? ForAssignedCancellations(int count, int total)
    {
        return total >= MinAssignedServices && count * 2 >= total
            ? new AccountAlertResponse(AccountAlertType.AssignedCancellations, Count: count, Total: total)
            : null;
    }

    public static AccountAlertResponse? ForLowAverageRating(decimal? averageRating, int reviewCount)
    {
        return averageRating <= MaxLowAverageRating && reviewCount >= MinReviews
            ? new AccountAlertResponse(
                AccountAlertType.LowAverageRating, AverageRating: averageRating, ReviewCount: reviewCount)
            : null;
    }
}

public enum AccountAlertType { AssignedCancellations, LowAverageRating }