using LocalHub.Application.Reports;
using LocalHub.Application.Reports.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api.Controllers;

// Activity reports: F013 (E33).
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportsController(ReportService reportService)
    {
        _reportService = reportService;
    }

    // E33: only clients, with their own data (providers and administrators get 403). The dates are read as text, so
    // that an invalid date is a global error of the operation and not an error of the field (API documentation, E33).
    [HttpGet("client")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientReportResponse>> GetClientReport(
        CancellationToken cancellationToken,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        return Ok(await _reportService.GetClientReportAsync(startDate, endDate, cancellationToken));
    }
}