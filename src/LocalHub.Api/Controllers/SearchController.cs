using System.ComponentModel.DataAnnotations;

using LocalHub.Application.Common;
using LocalHub.Application.Search;
using LocalHub.Application.Search.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// Searches by distance: F005 (E18) and, later, F006 (E19).
[ApiController]
[Route("api/search")]
public class SearchController(SearchService searchService) : ControllerBase
{
    // E18: public (RF-F001-03, rule 3). The parameters are validated by [ApiController] before the method is called
    // (rule R17). With the token of a client, its registered location is used when the request
    // has no coordinates and no address.
    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProviderSearchResult>>> SearchProviders(
        [FromQuery, Required(ErrorMessage = "Indique a categoria de serviços.")] int? serviceCategoryId,
        [FromQuery, StringLength(Domain.Accounts.User.AddressMaxLength, ErrorMessage = "A morada é demasiado longa.")] string? address,
        [FromQuery, Range(-90.0, 90.0, ErrorMessage = "A latitude tem de estar entre -90 e 90.")] decimal? latitude,
        [FromQuery, Range(-180.0, 180.0, ErrorMessage = "A longitude tem de estar entre -180 e 180.")] decimal? longitude,
        [FromQuery] string? name,
        CancellationToken cancellationToken,
        [FromQuery] int page = PageRequest.DefaultPage,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize)
    {
        var request = new ProviderSearchRequest(serviceCategoryId, address, latitude, longitude, name);
        return Ok(await searchService.SearchProvidersAsync(request, new PageRequest(page, pageSize), cancellationToken));
    }
}