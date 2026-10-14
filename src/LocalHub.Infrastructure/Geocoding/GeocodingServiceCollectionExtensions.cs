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
}