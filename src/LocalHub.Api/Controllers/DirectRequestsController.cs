using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F007: the direct requests received by the provider or sent by the client.
[ApiController]
[Route("api/direct-requests")]
public class DirectRequestsController(DirectRequestService directRequestService, ICurrentUser currentUser)
    : ControllerBase
{
    // E23: the provider sees the requests sent to its profile; the client, the requests of its services, with the
    // provider (DirectRequestListItem or ClientDirectRequestListItem).
    [HttpGet]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        var pageRequest = new PageRequest(page, pageSize);
        return currentUser.Role == UserRole.Client
            ? Ok(await directRequestService.ListForClientAsync(status, pageRequest, cancellationToken))
            : Ok(await directRequestService.ListAsync(status, pageRequest, cancellationToken));
    }

    // E24: only the provider that received the request.
    [HttpPost("{directRequestId:int}/decline")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<DirectRequestStatusResponse>> Decline(
        int directRequestId, CancellationToken cancellationToken)
    {
        return Ok(await directRequestService.DeclineAsync(directRequestId, cancellationToken));
    }
}