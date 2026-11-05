using System.Text.Json;

namespace LocalHub.Application.Common;

public static class EnumQuery
{
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