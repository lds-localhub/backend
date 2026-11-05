using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Reports;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Application.Reports;

// E33: the indicators of the activity report of the client (RF-F013-01) and its period (RF-F013-02).
[Trait("Category", "Unit")]
public class ClientReportTests
{

    // ---------- Period (RF-F013-02) ----------

    // Rule 3 (criterion 2).
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Parse_StartDateAfterEndDate_ThrowsGlobalError()
    {
        var exception = Assert.Throws<ValidationException>(() => ReportPeriod.Parse("2026-10-31", "2026-10-01"));

        Assert.Equal(ReportPeriod.InvertedMessage, exception.Message);
        Assert.Empty(exception.Errors);
    }

    // ---------- Helpers ----------

}