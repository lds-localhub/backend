using LocalHub.Domain.Accounts;

namespace LocalHub.Infrastructure.Security;

public interface ITokenIssuer
{
    string Issue(User user, UserSession session);
}