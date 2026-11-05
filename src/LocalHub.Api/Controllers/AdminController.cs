using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

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

    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        return Ok(await _accountService.GetAdminSummaryAsync(cancellationToken));
    }
}