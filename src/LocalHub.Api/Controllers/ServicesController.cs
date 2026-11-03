using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Reviews;
using LocalHub.Application.Reviews.Dtos;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F007 to F012: the services with direct requests, the published jobs and their reviews.
[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly ServiceWorkflowService _serviceWorkflowService;
    private readonly ReviewService _reviewService;
    private readonly ICurrentUser _currentUser;

    public ServicesController(
        ServiceWorkflowService serviceWorkflowService, ReviewService reviewService, ICurrentUser currentUser)
    {
        _serviceWorkflowService = serviceWorkflowService;
        _reviewService = reviewService;
        _currentUser = currentUser;
    }

    // E32: only the client that created the service (providers and administrators get 403). 201 without Location:
    // the reviews are consulted in E15.
    [HttpPost("{serviceId:int}/review")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ReviewResponse>> Review(
        int serviceId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var review = await _reviewService.CreateAsync(serviceId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, review);
    }

    // E20: only clients (providers and administrators get 403).
    [HttpPost]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientServiceResponse>> Create(
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

    // E30: the client that created the service (OPEN or ASSIGNED) or the provider whose proposal was accepted
    // (ASSIGNED). Administrators get 403.
    [HttpPost("{serviceId:int}/cancel")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ServiceStatusResponse>> Cancel(int serviceId, CancellationToken cancellationToken)
    {
        if (_currentUser.Role == UserRole.Client)
        {
            return Ok(await _serviceWorkflowService.CancelAsClientAsync(serviceId, cancellationToken));
        }

        return Ok(await _serviceWorkflowService.CancelAsProviderAsync(serviceId, cancellationToken));
    }

    // E31: only the client that created the service (providers and administrators get 403).
    [HttpPost("{serviceId:int}/complete")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<CompletedServiceResponse>> Complete(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.CompleteAsync(serviceId, cancellationToken));
    }

    // E25: only providers with a profile (clients and administrators get 403).
    [HttpPost("{serviceId:int}/proposals")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProposalResponse>> SubmitProposal(
        int serviceId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var proposal = await _serviceWorkflowService.SubmitProposalAsync(serviceId, request, cancellationToken);
        return Created($"/api/proposals/{proposal.ProposalId}", proposal);
    }
}