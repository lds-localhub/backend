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

// F007 to F012: the services with direct requests, the PUBLIC services and their reviews.
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

    // E21: only clients, with the services they created (providers and administrators get 403). The provider follows
    // its services through the direct requests (E23) and the proposals (E27).
    [HttpGet]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<PagedResult<ServiceListItem>>> ListMine(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _serviceWorkflowService.ListMineAsync(status, new PageRequest(page, pageSize), cancellationToken));
    }

    // E22: only the client that created the service (providers and administrators get 403). The provider consults the
    // service through its direct request (E36), its proposal (E37) or the PUBLIC service (E38).
    [HttpGet("{serviceId:int}")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientServiceResponse>> Get(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.GetAsync(serviceId, cancellationToken));
    }

    // E30: the client that created the service (OPEN or ASSIGNED) or the provider whose proposal was accepted
    // (ASSIGNED). Administrators get 403.
    [HttpPost("{serviceId:int}/cancel")]
    [Authorize(Roles = "CLIENT,PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
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
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<ProposalResponse>> SubmitProposal(
        int serviceId, SubmitProposalRequest request, CancellationToken cancellationToken)
    {
        var proposal = await _serviceWorkflowService.SubmitProposalAsync(serviceId, request, cancellationToken);
        return Created($"/api/proposals/{proposal.ProposalId}", proposal);
    }
}