using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Common;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    UserRole Role { get; }
    int SessionId { get; }
}