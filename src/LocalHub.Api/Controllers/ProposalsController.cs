using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F007 and F008: the proposals presented by the provider and the decisions of the client on them.
[ApiController]
[Route("api/proposals")]
public class ProposalsController : ControllerBase
{
    private readonly ServiceWorkflowService _serviceWorkflowService;

    public ProposalsController(ServiceWorkflowService serviceWorkflowService)
    {
        _serviceWorkflowService = serviceWorkflowService;
    }

    // E27: only providers, and only their own proposals.
    [HttpGet]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PagedResult<MyProposalListItem>>> ListMine(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _serviceWorkflowService.ListMyProposalsAsync(
            status, new PageRequest(page, pageSize), cancellationToken));
    }

    // E37: only the provider that presented the proposal.
    [HttpGet("{proposalId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProviderProposalResponse>> Get(int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.GetMyProposalAsync(proposalId, cancellationToken));
    }

    // E28: only the client that created the service of the proposal.
    [HttpPost("{proposalId:int}/accept")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ProposalDecisionResponse>> Accept(int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.AcceptProposalAsync(proposalId, cancellationToken));
    }

    // E29: only the client that created the service of the proposal.
    [HttpPost("{proposalId:int}/decline")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ProposalDecisionResponse>> Decline(int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.DeclineProposalAsync(proposalId, cancellationToken));
    }
}