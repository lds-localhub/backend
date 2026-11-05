using LocalHub.Application.Profiles.Dtos;

namespace LocalHub.Application.Search.Dtos;

public record ProviderSearchResult(
    int ProfessionalProfileId,
    string Name,
    IReadOnlyList<ProfileCategoryResponse> ServiceCategories,
    decimal? AverageRating,
    int ReviewCount,
    decimal DistanceKm,
    short ServiceRadiusKm,
    bool WithinServiceRadius);