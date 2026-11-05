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

    [HttpPost("{serviceId:int}/review")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ReviewResponse>> Review(
        int serviceId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var review = await _reviewService.CreateAsync(serviceId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, review);
    }

    [HttpPost]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientServiceResponse>> Create(
        CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var service = await _serviceWorkflowService.CreateAsync(request, cancellationToken);
        return Created($"/api/services/{service.ServiceId}", service);
    }

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

    [HttpGet("{serviceId:int}")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientServiceResponse>> Get(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.GetAsync(serviceId, cancellationToken));
    }

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

    [HttpPost("{serviceId:int}/complete")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<CompletedServiceResponse>> Complete(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _serviceWorkflowService.CompleteAsync(serviceId, cancellationToken));
    }

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