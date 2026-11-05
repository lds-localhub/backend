using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/proposals")]
public class ProposalsController : ControllerBase
{
    private readonly ServiceWorkflowService _serviceWorkflowService;

    public ProposalsController(ServiceWorkflowService serviceWorkflowService)
    {
        _serviceWorkflowService = serviceWorkflowService;
    }

    [HttpGet]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<PagedResult<MyProposalListItem>>> ListMine(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _serviceWorkflowService.ListMyProposalsAsync(
            status, new PageRequest(page, pageSize), cancellationToken));
    }

    [HttpGet("{proposalId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<ProviderProposalResponse>> Get(int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.GetMyProposalAsync(proposalId, cancellationToken));
    }

    [HttpPost("{proposalId:int}/accept")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ProposalDecisionResponse>> Accept(int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.AcceptProposalAsync(proposalId, cancellationToken));
    }

    [HttpPost("{proposalId:int}/decline")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ProposalDecisionResponse>> Decline(int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.DeclineProposalAsync(proposalId, cancellationToken));
    }
}