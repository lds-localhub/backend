namespace LocalHub.Application.Profiles.Dtos;

// Body of E12. The address is converted into coordinates and saved in User; serviceRadiusKm is a decimal so that
// 25.5 is rejected with a clear message (ServiceRadius) instead of a generic "invalid value".
public record CreateProfileRequest(
    string? Introduction,
    string? ContactDetails,
    string? Experience,
    IReadOnlyList<int>? ServiceCategoryIds,
    string? Address,
    decimal? ServiceRadiusKm);