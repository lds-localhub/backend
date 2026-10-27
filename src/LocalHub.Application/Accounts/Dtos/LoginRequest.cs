using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Accounts.Dtos;

// Body of E02. The fields are validated by [ApiController] before the controller is called (rule R17).
public record LoginRequest(
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    string? Email,

    [Required(ErrorMessage = "A palavra-passe é obrigatória.")]
    string? Password);