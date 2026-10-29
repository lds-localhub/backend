using System.ComponentModel.DataAnnotations;

using LocalHub.Application.Locations;
using LocalHub.Application.Locations.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// F015: the address of the position of the device, used by the mobile application (E35).
[ApiController]
[Route("api/geocoding")]
public class GeocodingController : ControllerBase
{
    private readonly LocationService _locationService;

    public GeocodingController(LocationService locationService)
    {
        _locationService = locationService;
    }

    // E35: public (RF-F001-03, rule 3). The coordinates are validated by [ApiController] before the method is called
    // (RF-F015-04, rule R17).
    [HttpGet("reverse")]
    [AllowAnonymous]
    public async Task<ActionResult<AddressResponse>> Reverse(
        [FromQuery, Required(ErrorMessage = "Indique a latitude."), Range(-90.0, 90.0, ErrorMessage = "A latitude tem de estar entre -90 e 90.")] decimal? latitude,
        [FromQuery, Required(ErrorMessage = "Indique a longitude."), Range(-180.0, 180.0, ErrorMessage = "A longitude tem de estar entre -180 e 180.")] decimal? longitude,
        CancellationToken cancellationToken)
    {
        return Ok(await _locationService.ReverseGeocodeAsync(latitude!.Value, longitude!.Value, cancellationToken));
    }
}