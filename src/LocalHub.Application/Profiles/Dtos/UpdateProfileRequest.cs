using System.Text.Json.Serialization;

namespace LocalHub.Application.Profiles.Dtos;

// Body of E14. The address and the radius are changed in E16 and E17. Fields that are not listed here, such as
// averageRating, are rejected with 400 (API documentation, E14; RF-F004-02, rule 3).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateProfileRequest(
    string? Introduction,
    string? ContactDetails,
    string? Experience,
    IReadOnlyList<int>? ServiceCategoryIds);