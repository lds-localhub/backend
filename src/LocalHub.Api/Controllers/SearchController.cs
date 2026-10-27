using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Search;
using LocalHub.Application.Search.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// Searches by distance: F005 (E18) and F006 (E19, with the detail of each published job in E38).
[ApiController]
[Route("api/search")]
public class SearchController(SearchService searchService) : ControllerBase
{
    // E18: public (RF-F001-03, rule 3). With the token of a client, its registered location is used when the request
    // has no coordinates and no address.
    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProviderSearchResult>>> SearchProviders(
        [FromQuery] int? serviceCategoryId,
        [FromQuery] string? address,
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        [FromQuery] string? name,
        CancellationToken cancellationToken,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        var request = new ProviderSearchRequest(serviceCategoryId, address, latitude, longitude, name);
        return Ok(await searchService.SearchProvidersAsync(request, new PageRequest(page, pageSize), cancellationToken));
    }

    // E19: only providers with a profile (clients and administrators get 403). The distance is always calculated from
    // the registered location of the provider.
    [HttpGet("services")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PagedResult<PublishedJobSearchResult>>> SearchPublishedJobs(
        CancellationToken cancellationToken,
        [FromQuery] int? serviceCategoryId = null,
        [FromQuery] bool withinServiceRadius = false,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await searchService.SearchPublishedJobsAsync(
            serviceCategoryId, withinServiceRadius, new PageRequest(page, pageSize), cancellationToken));
    }

    // E38: only providers with a profile, for the published jobs that E19 returns.
    [HttpGet("services/{serviceId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PublishedJobResponse>> GetPublishedJob(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await searchService.GetPublishedJobAsync(serviceId, cancellationToken));
    }
}