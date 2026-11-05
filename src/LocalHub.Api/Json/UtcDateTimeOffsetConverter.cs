using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalHub.Api.Json;

// Writes instants in ISO 8601 UTC, to the second, with the "Z" suffix (API documentation, section 4.3), for example
// 2026-10-08T18:00:00Z. The precision of .NET (100 ns) and of PostgreSQL (microseconds) does not reach the responses,
// so the instant returned by an action is the same that a later query returns.
public class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
}