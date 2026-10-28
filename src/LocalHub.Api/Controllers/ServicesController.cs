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
public class ServicesController : ControllerBase
{
    private readonly ServiceWorkflowService _serviceWorkflowService;
    private readonly ICurrentUser _currentUser;

    public ServicesController(ServiceWorkflowService serviceWorkflowService, ICurrentUser currentUser)
    {
        _serviceWorkflowService = serviceWorkflowService;
        _currentUser = currentUser;
    }

    // E20: only clients (providers and administrators get 403).
    [HttpPost]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ServiceResponse>> Create(
        CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var service = await _serviceWorkflowService.CreateAsync(request, cancellationToken);
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
        return Ok(await _serviceWorkflowService.ListMineAsync(
            status, proposalModel, new PageRequest(page, pageSize), cancellationToken));
    }

    // E22: each role sees a different view (ClientServiceResponse or ProviderServiceResponse); administrators get 403.
    [HttpGet("{serviceId:int}")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<IActionResult> Get(int serviceId, CancellationToken cancellationToken)
    {
        if (_currentUser.Role == UserRole.Client)
        {
            return Ok(await _serviceWorkflowService.GetForClientAsync(serviceId, cancellationToken));
        }

        return Ok(await _serviceWorkflowService.GetForProviderAsync(serviceId, cancellationToken));
    }

    // E26: only the client that created the service.
    [HttpGet("{serviceId:int}/proposals")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<IReadOnlyList<ClientProposalResponse>>> ListProposals(
        int serviceId, CancellationToken cancellationToken, [FromQuery] string? status = null)
    {
        return Ok(await _serviceWorkflowService.ListProposalsAsync(serviceId, status, cancellationToken));
    }

    // E28: only the client that created the service.
    [HttpPost("{serviceId:int}/proposals/{proposalId:int}/accept")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientServiceResponse>> AcceptProposal(
        int serviceId, int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.AcceptProposalAsync(serviceId, proposalId, cancellationToken));
    }

    // E29: only the client that created the service.
    [HttpPost("{serviceId:int}/proposals/{proposalId:int}/decline")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ProposalResponse>> DeclineProposal(
        int serviceId, int proposalId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.DeclineProposalAsync(serviceId, proposalId, cancellationToken));
    }

    // E25: only providers with a profile (clients and administrators get 403).
    [HttpPost("{serviceId:int}/proposals")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProposalResponse>> SubmitProposal(
        int serviceId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var proposal = await _serviceWorkflowService.SubmitProposalAsync(serviceId, request, cancellationToken);
        return Created($"/api/services/{serviceId}/proposals/{proposal.ProposalId}", proposal);
    }
}