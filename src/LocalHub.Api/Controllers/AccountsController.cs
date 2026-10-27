using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F002: account management, reserved to the administrator; other roles get 403 (RF-F002-01, RNF-03).
[ApiController]
[Route("api/accounts")]
[Authorize(Roles = "ADMIN")]
public class AccountsController : ControllerBase
{
    private readonly AccountService _accountService;

    public AccountsController(AccountService accountService)
    {
        _accountService = accountService;
    }

    // E05
    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountResponse>>> List(
        [FromQuery] string? role,
        [FromQuery] string? status,
        CancellationToken cancellationToken,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _accountService.ListAsync(role, status, new PageRequest(page, pageSize), cancellationToken));
    }

    // E06
    [HttpPost("{userId:int}/deactivate")]
    public async Task<ActionResult<AccountResponse>> Deactivate(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _accountService.DeactivateAsync(userId, cancellationToken));
    }

    // E07
    [HttpPost("{userId:int}/activate")]
    public async Task<ActionResult<AccountResponse>> Activate(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ActivateAsync(userId, cancellationToken));
    }

    // E41
    [HttpPost("{userId:int}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ResetPasswordAsync(userId, cancellationToken));
    }
}