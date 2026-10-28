using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Domain.Profiles;

namespace LocalHub.Application.Profiles.Dtos;

// Body of E14. The address and the radius are changed in E16 and E17. Fields that are not listed here, such as
// averageRating, are rejected with 400 (API documentation, E14; RF-F004-02, rule 3).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateProfileRequest(
    [Required(ErrorMessage = "Indique a apresentação.")]
    [StringLength(ProfessionalProfile.IntroductionMaxLength, ErrorMessage = "A apresentação é demasiado longa.")]
    string? Introduction,

    [Required(ErrorMessage = "Indique os contactos.")]
    [StringLength(ProfessionalProfile.ContactDetailsMaxLength, ErrorMessage = "Os contactos são demasiado longos.")]
    string? ContactDetails,

    [Required(ErrorMessage = "Indique a experiência profissional.")]
    [StringLength(ProfessionalProfile.ExperienceMaxLength,
        ErrorMessage = "A experiência profissional é demasiado longa.")]
    string? Experience,

    [Required(ErrorMessage = "Indique pelo menos uma categoria de serviços.")]
    [MinLength(1, ErrorMessage = "Indique pelo menos uma categoria de serviços.")]
    IReadOnlyList<int>? ServiceCategoryIds);