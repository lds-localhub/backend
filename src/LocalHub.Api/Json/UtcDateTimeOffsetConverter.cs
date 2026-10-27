using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalHub.Api.Json;

// Writes instants in ISO 8601 UTC with the "Z" suffix (API documentation, section 4.3).
public class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTimeOffset();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.UtcDateTime);
    }
}