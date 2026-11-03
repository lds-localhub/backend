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

    // E16. Only clients; administrators and providers get 403 (the address of a provider is set in E12 and E14,
    // RF-F015-01, rule 3).
    [HttpPut("me/location")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<UserResponse>> SetLocation(
        SetLocationRequest request, CancellationToken cancellationToken) =>
        Ok(await _locationService.SetMineAsync(request, cancellationToken));

    // E40. Any authenticated user (the fallback policy of Program.cs requires a session).
    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }
}