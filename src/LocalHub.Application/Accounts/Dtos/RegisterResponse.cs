using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

public record RegisterResponse(int UserId, string Name, string Email, UserRole Role, ActivationStatus Status);