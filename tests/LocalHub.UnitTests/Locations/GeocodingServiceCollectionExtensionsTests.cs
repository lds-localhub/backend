using LocalHub.Infrastructure.Geocoding;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalHub.UnitTests.Locations;

[Trait("Category", "Unit")]
public class GeocodingServiceCollectionExtensionsTests
{
    private const string UserAgent = "LocalHub/1.0 (LDS ESTG G14)";

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public void AddNominatimGeocoding_ConfiguracaoValida_ConfiguraOHttpClientComAPoliticaDoNominatim()
    {
        using ServiceProvider provider = BuildProvider(new() { ["Nominatim:UserAgent"] = UserAgent });

        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.IsType<NominatimGeocodingService>(provider.GetRequiredService<IGeocodingService>());

        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(GeocodingServiceCollectionExtensions.HttpClientName);
        Assert.Equal(new Uri("https://nominatim.openstreetmap.org/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
        Assert.Equal(UserAgent, client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal("application/json", client.DefaultRequestHeaders.Accept.ToString());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public void AddNominatimGeocoding_ValoresConfigurados_SubstituemOsPredefinidos()
    {
        using ServiceProvider provider = BuildProvider(new()
        {
            ["Nominatim:UserAgent"] = UserAgent,
            ["Nominatim:BaseUrl"] = "http://localhost:8080/nominatim/",
            ["Nominatim:Timeout"] = "00:00:05",
        });

        provider.GetRequiredService<IStartupValidator>().Validate();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(GeocodingServiceCollectionExtensions.HttpClientName);
        Assert.Equal(new Uri("http://localhost:8080/nominatim/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(5), client.Timeout);
    }

    [Theory]
    [Trait("Requisito", "RF-F015-03")]
    [InlineData("Nominatim:UserAgent", "")]
    [InlineData("Nominatim:UserAgent", "   ")]
    [InlineData("Nominatim:UserAgent", "LocalHub/1.0 (sem fecho")]
    [InlineData("Nominatim:BaseUrl", "https://servidor/nominatim")]
    [InlineData("Nominatim:MinIntervalBetweenRequests", "00:00:00.500")]
    [InlineData("Nominatim:Timeout", "00:05:00")]
    public void AddNominatimGeocoding_ConfiguracaoInvalida_FalhaNoArranque(string key, string value)
    {
        Dictionary<string, string?> settings = new() { ["Nominatim:UserAgent"] = UserAgent };
        settings[key] = value;
        using ServiceProvider provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    [Trait("Requisito", "RF-F015-03")]
    public void AddNominatimGeocoding_SemUserAgent_FalhaNoArranque()
    {
        using ServiceProvider provider = BuildProvider([]);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(exception.Failures, f => f.Contains("UserAgent", StringComparison.Ordinal));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNominatimGeocoding(configuration);
        return services.BuildServiceProvider();
    }
}