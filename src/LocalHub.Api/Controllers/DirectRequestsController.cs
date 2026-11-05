using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/direct-requests")]
public class DirectRequestsController : ControllerBase
{
    private readonly DirectRequestService _directRequestService;

    public DirectRequestsController(DirectRequestService directRequestService)
    {
        _directRequestService = directRequestService;
    }

    [HttpGet]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<PagedResult<DirectRequestListItem>>> List(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _directRequestService.ListAsync(status, new PageRequest(page, pageSize), cancellationToken));
    }

    [HttpGet("{directRequestId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<ProviderDirectRequestResponse>> Get(int directRequestId, CancellationToken cancellationToken)
    {
        return Ok(await _directRequestService.GetAsync(directRequestId, cancellationToken));
    }

    [HttpPost("{directRequestId:int}/decline")]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<DirectRequestStatusResponse>> Decline(
        int directRequestId, CancellationToken cancellationToken)
    {
        return Ok(await _directRequestService.DeclineAsync(directRequestId, cancellationToken));
    }
}