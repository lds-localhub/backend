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
public sealed partial class NominatimGeocodingService(
    HttpClient httpClient,
    NominatimRateLimiter rateLimiter,
    IOptions<NominatimOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    /// <summary>Tamanho máximo da morada guardada (coluna address varchar(255) do Modelo de Dados v01).</summary>
    public const int AddressMaxLength = 255;

    private readonly NominatimOptions _options = options.Value;

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

        string address = displayName.GetString()!.Trim();
        if (address.Length == 0)
        {
            return null;
        }

        return address.Length <= AddressMaxLength ? address : address[..AddressMaxLength];
    }

    private async Task<JsonDocument> SendAsync(string path, Dictionary<string, string?> query, CancellationToken cancellationToken)
    {
        Uri requestUri = BuildRelativeUri(path, query);

        await rateLimiter.WaitTurnAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogUnsuccessfulStatus(logger, (int)response.StatusCode, path);
                throw new GeocodingUnavailableException("O Nominatim respondeu com um código de erro.");
            }

            using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogRequestFailed(logger, ex, path);
            throw new GeocodingUnavailableException("Não foi possível contactar o Nominatim.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Tempo limite do HttpClient: não foi o pedido do utilizador a ser cancelado.
            LogRequestFailed(logger, ex, path);
            throw new GeocodingUnavailableException("O Nominatim não respondeu a tempo.", ex);
        }
        catch (JsonException ex)
        {
            LogRequestFailed(logger, ex, path);
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