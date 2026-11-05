using LocalHub.Application.Reports.Dtos;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports;

public record ContractedService(
    int ServiceId,
    int ServiceCategoryId,
    string CategoryName,
    ServiceStatus Status,
    DateTimeOffset? ActualCompletionDate,
    decimal AcceptedAmount,
    string ProviderName) : IContractedService;

public static class ClientReport
{
    public static ClientReportResponse Build(IReadOnlyCollection<ContractedService> services)
    {
        var completedHistory = services
            .Where(s => s.Status == ServiceStatus.Completed && s.ActualCompletionDate != null)
            .OrderByDescending(s => s.ActualCompletionDate)
            .ThenByDescending(s => s.ServiceId)
            .Select(s => new CompletedServiceItem(
                s.ServiceId,
                s.ActualCompletionDate!.Value,
                new ServiceCategorySummary(s.ServiceCategoryId, s.CategoryName),
                s.ProviderName,
                s.AcceptedAmount))
            .ToList();

        return new ClientReportResponse(
            services.Count,
            ContractedIndicators.ByCategory(services),
            ContractedIndicators.ByStatus(services),
            ContractedIndicators.TotalAcceptedAmount(services),
            completedHistory);
    }
}