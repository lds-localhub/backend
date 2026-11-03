namespace LocalHub.Application.Accounts.Dtos;

public record LoginResponse(string Token, UserResponse User);