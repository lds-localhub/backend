using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalHub.Api.Filters;

// Returns 403 perfil-em-falta while the provider has not created the professional profile.
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
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        await next();
    }
}