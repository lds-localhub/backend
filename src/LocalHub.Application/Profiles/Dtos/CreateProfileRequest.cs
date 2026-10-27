using System.ComponentModel.DataAnnotations;

using LocalHub.Domain.Accounts;
using LocalHub.Domain.Profiles;

namespace LocalHub.Application.Profiles.Dtos;

// Body of E12. The address is converted into coordinates and saved in User. A radius with decimal places (25.5) is
// not a whole number, so the JSON reader rejects it with 400.
public record CreateProfileRequest(
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
    IReadOnlyList<int>? ServiceCategoryIds,

    [Required(ErrorMessage = "A morada é obrigatória.")]
    [StringLength(User.AddressMaxLength, ErrorMessage = "A morada é demasiado longa.")]
    string? Address,

    [Required(ErrorMessage = "Indique o raio de atuação, em quilómetros.")]
    [Range(ProfessionalProfile.ServiceRadiusMinKm, ProfessionalProfile.ServiceRadiusMaxKm,
        ErrorMessage = "O raio de atuação tem de estar entre 1 e 100 km.")]
    short? ServiceRadiusKm);
