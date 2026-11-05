using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Accounts.Dtos;

public record LoginRequest(
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    string? Email,

    [Required(ErrorMessage = "A palavra-passe é obrigatória.")]
    string? Password,

    [Required(ErrorMessage = "Indique a aplicação (WEB ou MOBILE).")]
    LoginClient? Client);