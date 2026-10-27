using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F007: the direct requests received by the provider.
[ApiController]
[Route("api/direct-requests")]
public class DirectRequestsController : ControllerBase
{
    private readonly DirectRequestService _directRequestService;

    public DirectRequestsController(DirectRequestService directRequestService)
    {
        _directRequestService = directRequestService;
    }

    // E23: the provider side. The client side is not implemented yet, so a client gets 403.
    [HttpGet]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PagedResult<DirectRequestListItem>>> List(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _directRequestService.ListAsync(status, new PageRequest(page, pageSize), cancellationToken));
    }

    // E24: only the provider that received the request.
    [HttpPost("{directRequestId:int}/decline")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<DirectRequestStatusResponse>> Decline(
        int directRequestId, CancellationToken cancellationToken)
    {
        return Ok(await _directRequestService.DeclineAsync(directRequestId, cancellationToken));
    }
}