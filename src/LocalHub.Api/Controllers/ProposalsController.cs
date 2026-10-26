using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F008: the proposals presented by the provider.
[ApiController]
[Route("api/proposals")]
public class ProposalsController(ServiceWorkflowService serviceWorkflowService) : ControllerBase
{
    // E27: only providers, and only their own proposals.
    [HttpGet]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PagedResult<MyProposalListItem>>> ListMine(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] string? proposalModel = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await serviceWorkflowService.ListMyProposalsAsync(
            status, proposalModel, new PageRequest(page, pageSize), cancellationToken));
    }
}