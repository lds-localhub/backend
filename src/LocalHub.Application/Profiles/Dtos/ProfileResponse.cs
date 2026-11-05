using LocalHub.Domain.Profiles;

namespace LocalHub.Application.Profiles.Dtos;

public record ProfileResponse(
    int ProfessionalProfileId,
    int ServiceProviderId,
    string Name,
    string Introduction,
    string ContactDetails,
    string Experience,
    IReadOnlyList<ProfileCategoryResponse> ServiceCategories,
    string? Address,
    short ServiceRadiusKm,
    decimal? AverageRating,
    int ReviewCount)
{
    // The profile must be loaded with its provider and the categories of its areas.
    public static ProfileResponse From(ProfessionalProfile profile, int reviewCount)
    {
        var categories = ProfileCategoryResponse.InDisplayOrder(
            profile.ServiceAreas.Select(a => new ProfileCategoryResponse(a.ServiceCategoryId, a.ServiceCategory.Name)));

        return new ProfileResponse(
            profile.ProfessionalProfileId,
            profile.ServiceProviderId,
            profile.ServiceProvider.Name,
            profile.Introduction,
            profile.ContactDetails,
            profile.Experience,
            categories,
            profile.ServiceProvider.Address,
            profile.ServiceRadiusKm,
            profile.AverageRating,
            reviewCount);
    }
}

public record ProfileCategoryResponse(int ServiceCategoryId, string Name)
{
    public static IReadOnlyList<ProfileCategoryResponse> InDisplayOrder(IEnumerable<ProfileCategoryResponse> categories) =>
        categories
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .ThenBy(c => c.ServiceCategoryId)
            .ToList();
}