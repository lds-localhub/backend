using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

public record AccountResponse(int UserId, string Name, string Email, UserRole Role, ActivationStatus Status)
{
    public static AccountResponse From(User user)
    {
        return new AccountResponse(user.UserId, user.Name, user.Email, user.Role, user.Status);
    }
}