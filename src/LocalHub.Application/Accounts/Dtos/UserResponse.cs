using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

public record UserResponse(
    int UserId,
    string Name,
    string Email,
    UserRole Role,
    string? Address,
    int? ProfessionalProfileId)
{
    public static UserResponse From(User user, int? professionalProfileId)
    {
        return new UserResponse(
            user.UserId,
            user.Name,
            user.Email,
            user.Role,
            user.Address,
            professionalProfileId);
    }
}