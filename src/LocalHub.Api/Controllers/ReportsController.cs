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

    // E33: only clients, with their own data (providers and administrators get 403). The dates are read as text and
    // validated by ReportPeriod, so that an invalid date gets the message of the API in "errors", under the name of the
    // parameter (API documentation v04, section 4.6).
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