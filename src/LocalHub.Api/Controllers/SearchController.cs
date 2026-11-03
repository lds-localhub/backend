using System.ComponentModel.DataAnnotations;

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
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

    // E18: public (RF-F001-03, rule 3). The parameters are validated by [ApiController] before the method is called
    // (rule R17). The reference location is received as an address (rule R3); with the token of a client, its
    // registered location is used when the request has no address.
    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProviderSearchResult>>> SearchProviders(
        [FromQuery, Required(ErrorMessage = "Indique a categoria de serviços.")] int? serviceCategoryId,
        [FromQuery, StringLength(Domain.Accounts.User.AddressMaxLength, ErrorMessage = "A morada é demasiado longa.")] string? address,
        [FromQuery] string? name,
        CancellationToken cancellationToken,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        var request = new ProviderSearchRequest(serviceCategoryId, address, name);
        return Ok(await _searchService.SearchProvidersAsync(request, new PageRequest(page, pageSize), cancellationToken));
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
        return Ok(await _searchService.SearchPublishedJobsAsync(
            serviceCategoryId, withinServiceRadius, new PageRequest(page, pageSize), cancellationToken));
    }

    // E38: only providers with a profile, for the published jobs that E19 returns.
    [HttpGet("services/{serviceId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [RequiresProfessionalProfile]
    public async Task<ActionResult<PublishedJobResponse>> GetPublishedJob(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _searchService.GetPublishedJobAsync(serviceId, cancellationToken));
    }
}