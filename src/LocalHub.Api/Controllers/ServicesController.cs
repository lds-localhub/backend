using LocalHub.Api.Filters;
using LocalHub.Application.Services;
using LocalHub.Application.Services.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F007 to F011: the services with direct requests and the published jobs.
[ApiController]
[Route("api/services")]
public class ServicesController(ServiceWorkflowService serviceWorkflowService) : ControllerBase
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

    // E22: the provider side. The client side is not implemented yet, so a client gets 403.
    [HttpGet("{serviceId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<ProviderServiceResponse>> Get(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await serviceWorkflowService.GetForProviderAsync(serviceId, cancellationToken));
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