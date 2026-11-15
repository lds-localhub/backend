using LocalHub.Application.Accounts;
using LocalHub.Application.Common;

using Microsoft.AspNetCore.Authorization;

namespace LocalHub.Api.Middleware;

// Rejects with 401 the requests whose session was revoked or expired, or whose account is inactive.
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
        // Public endpoints (login, register, health) ignore the token, so an old one does not block them.
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await next(context);
            return;
        }

        if (_currentUser.IsAuthenticated
            && !await _authService.ValidateSessionAsync(_currentUser.SessionId, context.RequestAborted))
        {
            await ProblemResponses.WriteSessionInvalidAsync(context);
            return;
        }

        await next(context);
    }
}