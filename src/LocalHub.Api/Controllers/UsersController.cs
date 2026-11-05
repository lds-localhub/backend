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
    private readonly AuthService _authService;
    private readonly LocationService _locationService;

    public UsersController(AuthService authService, LocationService locationService)
    {
        _authService = authService;
        _locationService = locationService;
    }

    [HttpPut("me/location")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<UserResponse>> SetLocation(
        SetLocationRequest request, CancellationToken cancellationToken) =>
        Ok(await _locationService.SetMineAsync(request, cancellationToken));

    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }
}