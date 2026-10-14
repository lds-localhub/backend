using LocalHub.Application.Accounts;
using LocalHub.Application.Common;

namespace LocalHub.Api.Middleware;

// Rejects with 401 the requests whose session is revoked or expired, or whose account is inactive (RNF-02).
// Runs after the JWT authentication, so it only checks requests that carry a valid token.
public class SessionValidationMiddleware(AuthService authService, ICurrentUser currentUser) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (currentUser.IsAuthenticated
            && !await authService.ValidateSessionAsync(currentUser.SessionId, context.RequestAborted))
        {
            await ProblemResponses.WriteSessionInvalidAsync(context);
            return;
        }

        await next(context);
    }
}