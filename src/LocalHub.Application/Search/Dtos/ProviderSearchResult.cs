using LocalHub.Application.Profiles.Dtos;

namespace LocalHub.Application.Search.Dtos;

// One result of E18 (RF-F005-01, rule 4): distanceKm has one decimal place and withinServiceRadius tells whether the
// reference location is inside the service radius of the provider.
public record ProviderSearchResult(
    int ProfessionalProfileId,
    string Name,
    IReadOnlyList<ProfileCategoryResponse> ServiceCategories,
    decimal? AverageRating,
    decimal DistanceKm,
    short ServiceRadiusKm,
    bool WithinServiceRadius);