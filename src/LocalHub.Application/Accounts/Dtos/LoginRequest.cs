using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Accounts.Dtos;

// Body of E02. The fields are validated by [ApiController] before the controller is called (rule R17).
// Each application always sends its own value of Client (RF-F001-06).
public record LoginRequest(
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    string? Email,

    [Required(ErrorMessage = "A palavra-passe é obrigatória.")]
    string? Password,

    [Required(ErrorMessage = "Indique a aplicação (WEB ou MOBILE).")]
    LoginClient? Client);