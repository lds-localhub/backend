using System.Security.Claims;

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
        if (_currentUser.IsAuthenticated
            && !await _authService.ValidateSessionAsync(_currentUser.SessionId, context.RequestAborted))
        {
            // Public endpoints (login, register, health) treat the request as anonymous, so an old token
            // neither blocks them nor still identifies the user.
            if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() == null)
            {
                await ProblemResponses.WriteSessionInvalidAsync(context);
                return;
            }

            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        await next(context);
    }
}