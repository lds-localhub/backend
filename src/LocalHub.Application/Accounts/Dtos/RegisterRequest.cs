using System.ComponentModel.DataAnnotations;

using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

// Body of E01. The fields are validated by [ApiController] before the controller is called (rule R17).
public record RegisterRequest(
    [Required(ErrorMessage = "Indique o nome.")]
    [StringLength(User.NameMaxLength, ErrorMessage = "O nome é demasiado longo.")]
    string? Name,

    [Required(ErrorMessage = "Indique o e-mail.")]
    [EmailAddress(ErrorMessage = "O e-mail não é válido.")]
    [StringLength(User.EmailMaxLength, ErrorMessage = "O e-mail é demasiado longo.")]
    string? Email,

    [Required(ErrorMessage = "Indique a palavra-passe.")]
    [MinLength(8, ErrorMessage = "A palavra-passe tem de ter, no mínimo, 8 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "A palavra-passe tem de ter pelo menos uma letra e um algarismo.")]
    string? Password,

    [Required(ErrorMessage = "Indique o papel.")]
    [AllowedValues(UserRole.Client, UserRole.Provider,
        ErrorMessage = "O papel tem de ser Cliente ou Prestador de Serviços.")]
    UserRole? Role);
