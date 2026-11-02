using System.Globalization;
using System.Text;
using System.Text.Json;

using LocalHub.Infrastructure.Geocoding;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalHub.Infrastructure.Geocoding;

/// <summary>
/// Adaptador do Nominatim (OpenStreetMap) — Arquitetura v02, DEC-07.
/// Usa os endpoints «search» e «reverse» no formato jsonv2 e respeita a política de utilização:
/// User-Agent próprio (configurado no HttpClient) e no máximo um pedido por segundo (<see cref="NominatimRateLimiter"/>).
/// </summary>
public sealed partial class NominatimGeocodingService : IGeocodingService
{
    /// <summary>Tamanho máximo da morada guardada (coluna address varchar(255) do Modelo de Dados v01).</summary>
    public const int AddressMaxLength = 255;

    private readonly NominatimOptions _options;

    private readonly HttpClient _httpClient;
    private readonly NominatimRateLimiter _rateLimiter;
    private readonly ILogger<NominatimGeocodingService> _logger;

    public NominatimGeocodingService(
        HttpClient httpClient,
        NominatimRateLimiter rateLimiter,
        IOptions<NominatimOptions> options,
        ILogger<NominatimGeocodingService> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Coordinates?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        Dictionary<string, string?> query = new()
        {
            ["q"] = address.Trim(),
            ["format"] = "jsonv2",
            ["limit"] = "1",
            ["countrycodes"] = _options.CountryCodes,
            ["accept-language"] = _options.Language,
            ["email"] = _options.Email,
        };

        using JsonDocument document = await SendAsync("search", query, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement first = root[0];
        if (!TryReadDecimal(first, "lat", out decimal latitude) || !TryReadDecimal(first, "lon", out decimal longitude))
        {
            throw new GeocodingUnavailableException("O Nominatim devolveu coordenadas num formato inesperado.");
        }

        return Coordinates.Rounded(latitude, longitude);
    }

    public async Task<string?> ReverseGeocodeAsync(Coordinates coordinates, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string?> query = new()
        {
            ["lat"] = coordinates.Latitude.ToString(CultureInfo.InvariantCulture),
            ["lon"] = coordinates.Longitude.ToString(CultureInfo.InvariantCulture),
            ["format"] = "jsonv2",
            ["addressdetails"] = "1",
            ["accept-language"] = _options.Language,
            ["email"] = _options.Email,
        };

        using JsonDocument document = await SendAsync("reverse", query, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        // Sem morada, o Nominatim responde 200 com { "error": "Unable to geocode" }.
        if (root.ValueKind != JsonValueKind.Object
            || root.TryGetProperty("error", out _)
            || !root.TryGetProperty("display_name", out JsonElement displayName)
            || displayName.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        // E35 (API v04): street, number, postal code and locality, so that converting it back into coordinates
        // (E12, E14, E16, E18, E20) gives the same position. Without a street, the full display_name is used.
        string address = (ComposeAddress(root) ?? displayName.GetString()!).Trim();
        if (address.Length == 0)
        {
            return null;
        }

        return address.Length <= AddressMaxLength ? address : address.Substring(0, AddressMaxLength);
    }

    // "Rua de Santa Catarina 74, 4000-441 Porto", from the "address" object returned with addressdetails=1.
    private static string? ComposeAddress(JsonElement root)
    {
        if (!root.TryGetProperty("address", out JsonElement details) || details.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? road = ReadString(details, "road");
        if (road is null)
        {
            return null;
        }

        string street = ReadString(details, "house_number") is { } number ? $"{road} {number}" : road;
        string? locality = ReadString(details, "city") ?? ReadString(details, "town")
            ?? ReadString(details, "village") ?? ReadString(details, "municipality");
        string place = string.Join(' ', new[] { ReadString(details, "postcode"), locality }.Where(p => p is not null));

        return place.Length == 0 ? street : $"{street}, {place}";
    }

    private static string? ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
    }

    private async Task<JsonDocument> SendAsync(string path, Dictionary<string, string?> query, CancellationToken cancellationToken)
    {
        Uri requestUri = BuildRelativeUri(path, query);

        await _rateLimiter.WaitTurnAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogUnsuccessfulStatus(_logger, (int)response.StatusCode, path);
                throw new GeocodingUnavailableException("O Nominatim respondeu com um código de erro.");
            }

            using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogRequestFailed(_logger, ex, path);
            throw new GeocodingUnavailableException("Não foi possível contactar o Nominatim.", ex);
        }
        catch (TaskCanceledException ex)
        {
            // O pedido do utilizador foi cancelado: a exceção continua.
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            // Tempo limite do HttpClient: não foi o pedido do utilizador a ser cancelado.
            LogRequestFailed(_logger, ex, path);
            throw new GeocodingUnavailableException("O Nominatim não respondeu a tempo.", ex);
        }
        catch (JsonException ex)
        {
            LogRequestFailed(_logger, ex, path);
            throw new GeocodingUnavailableException("O Nominatim devolveu uma resposta inválida.", ex);
        }
    }

    private static Uri BuildRelativeUri(string path, Dictionary<string, string?> query)
    {
        StringBuilder builder = new(path);
        char separator = '?';

        foreach ((string key, string? value) in query)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder.Append(separator).Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return new Uri(builder.ToString(), UriKind.Relative);
    }

    private static bool TryReadDecimal(JsonElement element, string property, out decimal value)
    {
        value = 0m;
        return element.TryGetProperty(property, out JsonElement text)
            && text.ValueKind == JsonValueKind.String
            && decimal.TryParse(text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "O Nominatim respondeu {StatusCode} ao pedido {Path}.")]
    private static partial void LogUnsuccessfulStatus(ILogger logger, int statusCode, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha no pedido {Path} ao Nominatim.")]
    private static partial void LogRequestFailed(ILogger logger, Exception exception, string path);
}