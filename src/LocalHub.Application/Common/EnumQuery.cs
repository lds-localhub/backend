using System.Text.Json;

namespace LocalHub.Application.Common;

// Filter of a list by the value of an enum, written as in the API documentation (for example PROPOSAL_SUBMITTED).
public static class EnumQuery
{
    // Returns null when the filter is not used. An invalid value adds an error to the field.
    public static T? Parse<T>(string? value, string field, IDictionary<string, string[]> errors)
        where T : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        foreach (var candidate in Enum.GetValues<T>())
        {
            if (string.Equals(JsonNamingPolicy.SnakeCaseUpper.ConvertName(candidate.ToString()), value, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        errors[field] = new[] { "Valor inválido." };
        return null;
    }
}