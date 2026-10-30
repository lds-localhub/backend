using LocalHub.Application.Reports.Dtos;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports;

// A contracted service of the client (it reached ASSIGNED, so it has an ACCEPTED proposal), with the data of the
// report: the category, the current state and the accepted proposal.
public record ContractedService(
    int ServiceId,
    int ServiceCategoryId,
    string CategoryName,
    ServiceStatus Status,
    DateTimeOffset? ActualCompletionDate,
    decimal AcceptedAmount,
    string ProviderName) : IContractedService;

// The indicators of E33 (RF-F013-01; API documentation v04), calculated from the contracted services already filtered
// by client and period.
public static class ClientReport
{
    public static ClientReportResponse Build(IReadOnlyCollection<ContractedService> services)
    {
        // Rule 4: only the COMPLETED services, the most recent first.
        var completedHistory = services
            .Where(s => s.Status == ServiceStatus.Completed && s.ActualCompletionDate is not null)
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