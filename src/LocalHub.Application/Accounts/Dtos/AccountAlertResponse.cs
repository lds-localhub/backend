using System.Text.Json.Serialization;

namespace LocalHub.Application.Accounts.Dtos;

// Conduct alert of an account in E05 (RF-F002-01). Only the type and the values are returned; the text is formatted
// by the web application. Each type has only its own fields, so the others are left out of the response.
public record AccountAlertResponse(
    AccountAlertType Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Total = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AverageRating = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ReviewCount = null);

public enum AccountAlertType { AssignedCancellations, LowAverageRating }
