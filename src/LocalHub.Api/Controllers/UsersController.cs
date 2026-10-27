using LocalHub.Api.Filters;
using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Locations;
using LocalHub.Application.Locations.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly LocationService _locationService;

    public UsersController(AccountService accountService, LocationService locationService)
    {
        _accountService = accountService;
        _locationService = locationService;
    }

    // E04
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken) =>
        Ok(await _accountService.GetMeAsync(cancellationToken));

    // E16. Only clients and providers with a profile; administrators get 403 and providers without a profile
    // get 403 perfil-em-falta (the first location of a provider is set in E12).
    [HttpPut("me/location")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<UserResponse>> SetLocation(
        SetLocationRequest request, CancellationToken cancellationToken) =>
        Ok(await _locationService.SetMineAsync(request, cancellationToken));
}