namespace LocalHub.Application.Accounts.Dtos;

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserResponse User);