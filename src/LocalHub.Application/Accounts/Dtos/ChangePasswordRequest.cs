using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Accounts.Dtos;

// The confirmation is checked by the frontends and is not sent.
public record ChangePasswordRequest(
    [Required(ErrorMessage = "Indique a palavra-passe atual.")]
    string? CurrentPassword,

    [Required(ErrorMessage = "Indique a nova palavra-passe.")]
    [MinLength(8, ErrorMessage = "A palavra-passe tem de ter, no mínimo, 8 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "A palavra-passe tem de ter pelo menos uma letra e um algarismo.")]
    string? NewPassword);