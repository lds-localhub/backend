using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalHub.Api.Filters;

// "Profile filter" of the test plan: while a provider has not created the professional profile (E12), the endpoints
// marked with [ServiceFilter(typeof(RequireProfessionalProfileFilter))] return 403 perfil-em-falta (API documentation,
// section 7; RF-F004-01, rule 5; rule R11 of the Class Model). Other roles are not affected. It is an action filter,
// registered as Scoped in Program.cs, so it receives its dependencies in the constructor.
public class RequireProfessionalProfileFilter : IAsyncActionFilter
{
    private readonly ICurrentUser _currentUser;
    private readonly ProfileService _profileService;

    public RequireProfessionalProfileFilter(ICurrentUser currentUser, ProfileService profileService)
    {
        _currentUser = currentUser;
        _profileService = profileService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_currentUser.IsAuthenticated && _currentUser.Role == UserRole.Provider
            && await _profileService.GetMyProfileIdAsync(context.HttpContext.RequestAborted) == null)
        {
            // Converted into the 403 response by the ErrorHandlingMiddleware.
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        await next();
    }
}