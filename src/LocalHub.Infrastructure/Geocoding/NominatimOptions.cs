using System.ComponentModel.DataAnnotations;

namespace LocalHub.Infrastructure.Geocoding;

public sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";

    [Required]
    public Uri BaseUrl { get; set; } = new Uri("https://nominatim.openstreetmap.org/");

    [Required(AllowEmptyStrings = false)]
    public string UserAgent { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string CountryCodes { get; set; } = "pt";

    public string Language { get; set; } = "pt-PT";

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan MinIntervalBetweenRequests { get; set; } = TimeSpan.FromSeconds(1);

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}