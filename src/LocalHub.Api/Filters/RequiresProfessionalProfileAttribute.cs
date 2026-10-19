using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalHub.Api.Filters;

// "Profile filter" of the test plan: while a provider has not created the professional profile (E12), the endpoints
// marked with this attribute return 403 perfil-em-falta (API documentation, section 7; RF-F004-01, rule 5).
// Other roles are not affected. It is a resource filter, so it runs after [Authorize] (wrong role → 403 first)
// and before the body is read (no 400 for a provider that cannot use the endpoint anyway).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresProfessionalProfileAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var currentUser = services.GetRequiredService<ICurrentUser>();

        if (currentUser.IsAuthenticated && currentUser.Role == UserRole.Provider)
        {
            var profileService = services.GetRequiredService<ProfileService>();
            if (await profileService.GetMyProfileIdAsync(context.HttpContext.RequestAborted) is null)
            {
                // Converted into the 403 response by the ErrorHandlingMiddleware.
                throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
            }
        }

        await next();
    }
}