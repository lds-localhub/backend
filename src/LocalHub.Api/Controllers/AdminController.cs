using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F002 and F003: the summary of the administration, reserved to the administrator; other roles get 403 (RNF-03).
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly AccountService _accountService;

    public AdminController(AccountService accountService)
    {
        _accountService = accountService;
    }

    // E39
    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        return Ok(await _accountService.GetAdminSummaryAsync(cancellationToken));
    }
}