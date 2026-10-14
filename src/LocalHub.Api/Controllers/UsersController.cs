using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(AccountService accountService) : ControllerBase
{
    // E04
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken) =>
        Ok(await accountService.GetMeAsync(cancellationToken));
}