using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalHub.Api.Json;

// Writes instants in UTC, to the second, with the "Z" suffix (e.g. 2026-10-08T18:00:00Z).
public class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
}