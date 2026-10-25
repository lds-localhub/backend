using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F007 to F011: the services with direct requests and the published jobs.
[ApiController]
[Route("api/services")]
public class ServicesController(ServiceWorkflowService serviceWorkflowService, ICurrentUser currentUser)
    : ControllerBase
{
    // E20: only clients (providers and administrators get 403).
    [HttpPost]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ServiceResponse>> Create(
        CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var service = await serviceWorkflowService.CreateAsync(request, cancellationToken);
        return Created($"/api/services/{service.ServiceId}", service);
    }

    // E21: the client sees the services it created; the provider, those in which its proposal was accepted.
    [HttpGet]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PagedResult<ServiceListItem>>> ListMine(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] string? proposalModel = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await serviceWorkflowService.ListMineAsync(
            status, proposalModel, new PageRequest(page, pageSize), cancellationToken));
    }

    // E22: each role sees a different view (ClientServiceResponse or ProviderServiceResponse); administrators get 403.
    [HttpGet("{serviceId:int}")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<IActionResult> Get(int serviceId, CancellationToken cancellationToken)
    {
        return currentUser.Role == UserRole.Client
            ? Ok(await serviceWorkflowService.GetForClientAsync(serviceId, cancellationToken))
            : Ok(await serviceWorkflowService.GetForProviderAsync(serviceId, cancellationToken));
    }

    // E26: only the client that created the service.
    [HttpGet("{serviceId:int}/proposals")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<IReadOnlyList<ClientProposalResponse>>> ListProposals(
        int serviceId, CancellationToken cancellationToken, [FromQuery] string? status = null)
    {
        return Ok(await serviceWorkflowService.ListProposalsAsync(serviceId, status, cancellationToken));
    }

    // E28: only the client that created the service.
    [HttpPost("{serviceId:int}/proposals/{proposalId:int}/accept")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientServiceResponse>> AcceptProposal(
        int serviceId, int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await serviceWorkflowService.AcceptProposalAsync(serviceId, proposalId, cancellationToken));
    }

    // E29: only the client that created the service.
    [HttpPost("{serviceId:int}/proposals/{proposalId:int}/decline")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ProposalResponse>> DeclineProposal(
        int serviceId, int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await serviceWorkflowService.DeclineProposalAsync(serviceId, proposalId, cancellationToken));
    }

    // E25: only providers with a profile (clients and administrators get 403).
    [HttpPost("{serviceId:int}/proposals")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProposalResponse>> SubmitProposal(
        int serviceId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var proposal = await serviceWorkflowService.SubmitProposalAsync(serviceId, request, cancellationToken);
        return Created($"/api/services/{serviceId}/proposals/{proposal.ProposalId}", proposal);
    }
}