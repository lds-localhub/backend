using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalHub.Infrastructure.Geocoding;

public static class GeocodingServiceCollectionExtensions
{
    public const string HttpClientName = "Nominatim";

    public static IServiceCollection AddNominatimGeocoding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NominatimOptions>()
            .Bind(configuration.GetSection(NominatimOptions.SectionName))
            .ValidateDataAnnotations()
            // The data annotations do not check these two, and both would only fail at the first request.
            .Validate(o => o.BaseUrl.IsAbsoluteUri && o.BaseUrl.AbsolutePath.EndsWith('/'),
                "Nominatim:BaseUrl tem de ser um endereço absoluto terminado em '/'.")
            .Validate(o => IsValidUserAgent(o.UserAgent),
                "Nominatim:UserAgent não é um User-Agent válido (por exemplo, \"LocalHub/1.0 (contacto)\").")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<NominatimRateLimiter>();

        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(HttpClientName, (provider, client) =>
        {
            NominatimOptions options = provider.GetRequiredService<IOptions<NominatimOptions>>().Value;
            client.BaseAddress = options.BaseUrl;
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        return services;
    }

    // Same parsing as DefaultRequestHeaders.UserAgent.ParseAdd, but without throwing.
    private static bool IsValidUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        using var request = new HttpRequestMessage();
        return request.Headers.UserAgent.TryParseAdd(userAgent);
    }
}