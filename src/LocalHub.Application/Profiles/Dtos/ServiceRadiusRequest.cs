using System.ComponentModel.DataAnnotations;

using LocalHub.Domain.Profiles;

namespace LocalHub.Application.Profiles.Dtos;

// Body of E17. A radius with decimal places (25.5) is not a whole number, so the JSON reader rejects it with 400.
public record ServiceRadiusRequest(
    [Required(ErrorMessage = "Indique o raio de atuação, em quilómetros.")]
    [Range(ProfessionalProfile.ServiceRadiusMinKm, ProfessionalProfile.ServiceRadiusMaxKm,
        ErrorMessage = "O raio de atuação tem de estar entre 1 e 100 km.")]
    short? ServiceRadiusKm);