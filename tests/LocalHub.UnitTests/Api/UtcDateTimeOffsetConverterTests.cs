using System.Text.Json;

using LocalHub.Api.Json;

namespace LocalHub.UnitTests.Api;

// The instants of the responses in ISO 8601 UTC, to the second, with the "Z" suffix (API documentation, section 4.3).
[Trait("Category", "Unit")]
public class UtcDateTimeOffsetConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcDateTimeOffsetConverter() } };

    [Fact]
    public void Write_InstanteComFracoesEOutroFuso_EscreveEmUtcAoSegundo()
    {
        // 18:30:45.1234567 at +01:00 is 17:30:45 UTC.
        var value = new DateTimeOffset(2026, 10, 8, 18, 30, 45, TimeSpan.FromHours(1)).AddTicks(1234567);

        string json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"2026-10-08T17:30:45Z\"", json);
    }

    [Fact]
    public void Write_InstanteNulo_EscreveNull()
    {
        string json = JsonSerializer.Serialize<DateTimeOffset?>(null, Options);

        Assert.Equal("null", json);
    }

    [Fact]
    public void Read_InstanteComFracoes_MantemOValorCompleto()
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-10-08T17:30:45.1234567Z\"", Options);

        Assert.Equal(new DateTimeOffset(2026, 10, 8, 17, 30, 45, TimeSpan.Zero).AddTicks(1234567), value);
    }
}