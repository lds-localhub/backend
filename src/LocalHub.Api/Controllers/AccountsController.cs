using LocalHub.Application.Accounts;
using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

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

    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountListItemResponse>>> List(
        [FromQuery] string? role,
        [FromQuery] string? status,
        CancellationToken cancellationToken,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _accountService.ListAsync(role, status, new PageRequest(page, pageSize), cancellationToken));
    }

    [HttpPost("{userId:int}/deactivate")]
    public async Task<ActionResult<AccountResponse>> Deactivate(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _accountService.DeactivateAsync(userId, cancellationToken));
    }

    [HttpPost("{userId:int}/activate")]
    public async Task<ActionResult<AccountResponse>> Activate(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ActivateAsync(userId, cancellationToken));
    }

    [HttpPost("{userId:int}/reset-password")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(int userId, CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ResetPasswordAsync(userId, cancellationToken));
    }
}