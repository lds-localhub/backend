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
    // professionalProfileId: null for clients, administrators and providers without a profile (API documentation, E04).
    public static UserResponse From(User user, int? professionalProfileId)
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
            professionalProfileId);
    }
}