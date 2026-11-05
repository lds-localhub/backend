using System.Text.Json.Serialization;

namespace LocalHub.Application.Accounts.Dtos;

// Conduct alert of an account in E05 (RF-F002-01). Only the type and the values are returned; the text is formatted
// by the web application. Each type has only its own fields, so the others are left out of the response.
public record AccountAlertResponse(
    AccountAlertType Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AverageRating = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ReviewCount = null)
{
    // Thresholds of the conduct alerts (RF-F002-01, rules 5 to 7).
    public const int MinAssignedServices = 3;
    public const int MinReviews = 3;
    public const decimal MaxLowAverageRating = 2.0m;

    // ASSIGNED_CANCELLATIONS: included with at least 3 assigned services and count / total >= 50% (without rounding).
    public static AccountAlertResponse? ForAssignedCancellations(int count, int total)
    {
        return total >= MinAssignedServices && count * 2 >= total
            ? new AccountAlertResponse(AccountAlertType.AssignedCancellations, Count: count, Total: total)
            : null;
    }

    // LOW_AVERAGE_RATING: included with an average rating <= 2.0 and at least 3 reviews; null without reviews.
    public static AccountAlertResponse? ForLowAverageRating(decimal? averageRating, int reviewCount)
    {
        return averageRating <= MaxLowAverageRating && reviewCount >= MinReviews
            ? new AccountAlertResponse(
                AccountAlertType.LowAverageRating, AverageRating: averageRating, ReviewCount: reviewCount)
            : null;
    }
}

public enum AccountAlertType { AssignedCancellations, LowAverageRating }