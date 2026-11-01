using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports.Dtos;

// E33 (RF-F013-01, RF-F013-02): the activity report of the client (API documentation v04, E33).
public record ClientReportResponse(
    int ContractedServices,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<StatusCount> ByStatus,
    decimal TotalAcceptedAmount,
    IReadOnlyList<CompletedServiceItem> CompletedHistory);

public record CategoryCount(int ServiceCategoryId, string Name, int Count);

public record StatusCount(ServiceStatus Status, int Count);

public record CompletedServiceItem(
    int ServiceId,
    DateTimeOffset ActualCompletionDate,
    ServiceCategorySummary ServiceCategory,
    string ProviderName,
    decimal Amount);