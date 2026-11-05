using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports.Dtos;

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