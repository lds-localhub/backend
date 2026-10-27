using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Accounts.Dtos;

// Body of E40. The fields are validated by [ApiController] before the controller is called (rule R17).
// The new password follows the same rules as the registration (RF-F001-01, rule 3); the confirmation is checked by
// the web and mobile applications and is not sent.
public record ChangePasswordRequest(
    [Required(ErrorMessage = "Indique a palavra-passe atual.")]
    string? CurrentPassword,

    [Required(ErrorMessage = "Indique a nova palavra-passe.")]
    [MinLength(8, ErrorMessage = "A palavra-passe tem de ter, no mínimo, 8 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "A palavra-passe tem de ter pelo menos uma letra e um algarismo.")]
    string? NewPassword);