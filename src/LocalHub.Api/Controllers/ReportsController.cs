using LocalHub.Api.Filters;
using LocalHub.Application.Reports;
using LocalHub.Application.Reports.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportsController(ReportService reportService)
    {
        _reportService = reportService;
    }

    // The dates are read as text so that an invalid date gets the API's own message.
    [HttpGet("client")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientReportResponse>> GetClientReport(
        CancellationToken cancellationToken,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        return Ok(await _reportService.GetClientReportAsync(startDate, endDate, cancellationToken));
    }

    [HttpGet("provider")]
    [Authorize(Roles = "PROVIDER")]
    [ServiceFilter(typeof(RequireProfessionalProfileFilter))]
    public async Task<ActionResult<ProviderReportResponse>> GetProviderReport(
        CancellationToken cancellationToken,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        return Ok(await _reportService.GetProviderReportAsync(startDate, endDate, cancellationToken));
    }
}