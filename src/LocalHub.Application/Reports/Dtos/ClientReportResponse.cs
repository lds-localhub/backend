using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports.Dtos;

// E33 (RF-F013-01, RF-F013-02): the activity report of the client (API documentation, E33).
public record ClientReportResponse(
    ReportPeriodResponse Period,
    int ContractedServices,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<StatusCount> ByStatus,
    decimal TotalAcceptedAmount,
    IReadOnlyList<CompletedServiceItem> CompletedHistory);

// The dates of the request; null when not indicated.
public record ReportPeriodResponse(DateOnly? StartDate, DateOnly? EndDate);

public record CategoryCount(int ServiceCategoryId, string Name, int Count);

public record StatusCount(ServiceStatus Status, int Count);

public record CompletedServiceItem(
    int ServiceId,
    string ServiceCategory,
    string Provider,
    decimal Amount,
    DateTimeOffset ActualCompletionDate);