using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

public record UserResponse(
    int UserId,
    string Name,
    string Email,
    UserRole Role,
    ActivationStatus Status,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? ProfessionalProfileId)
{
    // The professional profile (F004) does not exist yet, so this is always null for now.
    public static UserResponse From(User user)
    {
        return new UserResponse(
            user.UserId,
            user.Name,
            user.Email,
            user.Role,
            user.Status,
            user.Address,
            user.Latitude,
            user.Longitude,
            null);
    }
}