using System.Globalization;
using System.Security.Claims;

using LocalHub.Application.Common;
using LocalHub.Domain.Accounts;

namespace LocalHub.Api;

// Reads the authenticated user from the claims of the JWT (sub, role, sid).
public class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            return user != null && user.Identity != null && user.Identity.IsAuthenticated;
        }
    }

    public int UserId => ReadInt("sub");

    public int SessionId => ReadInt("sid");

    public UserRole Role
    {
        get
        {
            // The token stores "CLIENT", "PROVIDER" or "ADMIN".
            return Enum.Parse<UserRole>(ReadClaim("role"), ignoreCase: true);
        }
    }

    private string ReadClaim(string claimType)
    {
        return httpContextAccessor.HttpContext?.User.FindFirstValue(claimType) ?? string.Empty;
    }

    private int ReadInt(string claimType)
    {
        return int.Parse(ReadClaim(claimType), CultureInfo.InvariantCulture);
    }
}