using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Reports;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Application.Reports;

[Trait("Category", "Unit")]
public class ClientReportTests
{
    [Fact]
    [Trait("Requisito", "RF-F013-02")]
    public void Parse_StartDateAfterEndDate_ThrowsGlobalError()
    {
        var exception = Assert.Throws<ValidationException>(() => ReportPeriod.Parse("2026-10-31", "2026-10-01"));

        Assert.Equal(ReportPeriod.InvertedMessage, exception.Message);
        Assert.Empty(exception.Errors);
    }
}