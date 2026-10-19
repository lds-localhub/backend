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
}