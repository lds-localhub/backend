using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Accounts.Dtos;

// The authenticated user in E02, E04 and E16 (API documentation, E04). The coordinates are not returned: they are only
// used by the backend, to calculate the distances.
public record UserResponse(
    int UserId,
    string Name,
    string Email,
    UserRole Role,
    string? Address,
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
            user.Address,
            professionalProfileId);
    }
}