using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Locations;
using LocalHub.Application.Locations.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(AccountService accountService, LocationService locationService) : ControllerBase
{
    // E04
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken) =>
        Ok(await accountService.GetMeAsync(cancellationToken));

    // E16. Only clients and providers have a location; administrators get 403.
    [HttpPut("me/location")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    public async Task<ActionResult<UserResponse>> SetLocation(
        SetLocationRequest request, CancellationToken cancellationToken) =>
        Ok(await locationService.SetMineAsync(request, cancellationToken));
}