using System.ComponentModel.DataAnnotations;

using LocalHub.Api.Filters;
using LocalHub.Application.Common;
using LocalHub.Application.Search;
using LocalHub.Application.Search.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

    // Public, but with a client's token the registered location is used when no address is given.
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

    [HttpGet("services")]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<PagedResult<OpenServiceSearchResult>>> SearchServices(
        CancellationToken cancellationToken,
        [FromQuery] int? serviceCategoryId = null,
        [FromQuery] bool withinServiceRadius = false,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        return Ok(await _searchService.SearchOpenServicesAsync(
            serviceCategoryId, withinServiceRadius, new PageRequest(page, pageSize), cancellationToken));
    }

    [HttpGet("services/{serviceId:int}")]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<OpenServiceResponse>> GetService(int serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _searchService.GetOpenServiceAsync(serviceId, cancellationToken));
    }
}