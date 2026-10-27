using LocalHub.Application.Accounts;
using LocalHub.Application.Common;

namespace LocalHub.Api.Middleware;

// Rejects with 401 the requests whose session is revoked or expired, or whose account is inactive (RNF-02).
// Runs after the JWT authentication, so it only checks requests that carry a valid token.
public class SessionValidationMiddleware : IMiddleware
{
    private readonly AuthService _authService;
    private readonly ICurrentUser _currentUser;

    public SessionValidationMiddleware(AuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (_currentUser.IsAuthenticated
            && !await _authService.ValidateSessionAsync(_currentUser.SessionId, context.RequestAborted))
        {
            await ProblemResponses.WriteSessionInvalidAsync(context);
            return;
        }

        await next(context);
    }
}