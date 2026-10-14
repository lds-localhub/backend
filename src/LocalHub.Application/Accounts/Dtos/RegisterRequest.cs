using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

public record RegisterRequest(string? Name, string? Email, string? Password, UserRole? Role);