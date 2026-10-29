using LocalHub.Application.Reports.Dtos;
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
    string ProviderName);

// The indicators of E33 (RF-F013-01), calculated from the contracted services already filtered by client and period.
public static class ClientReport
{
    // The states that a contracted service can have, in the order of the life cycle (section 10).
    private static readonly ServiceStatus[] ContractedStates =
    {
        ServiceStatus.Assigned,
        ServiceStatus.Completed,
        ServiceStatus.Cancelled,
    };

    public static ClientReportResponse Build(ReportPeriod period, IReadOnlyCollection<ContractedService> services)
    {
        // Rule 1: one line per category with contracted services, the most frequent first.
        var byCategory = services
            .GroupBy(s => new { s.ServiceCategoryId, s.CategoryName })
            .Select(g => new CategoryCount(g.Key.ServiceCategoryId, g.Key.CategoryName, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        // Rule 2: always the three states, also with 0, so that the client sees the whole distribution.
        var byStatus = ContractedStates
            .Select(state => new StatusCount(state, services.Count(s => s.Status == state)))
            .ToList();

        // Rule 4: only the COMPLETED services, the most recent first.
        var completedHistory = services
            .Where(s => s.Status == ServiceStatus.Completed && s.ActualCompletionDate is not null)
            .OrderByDescending(s => s.ActualCompletionDate)
            .ThenByDescending(s => s.ServiceId)
            .Select(s => new CompletedServiceItem(
                s.ServiceId, s.CategoryName, s.ProviderName, s.AcceptedAmount, s.ActualCompletionDate!.Value))
            .ToList();

        return new ClientReportResponse(
            new ReportPeriodResponse(period.StartDate, period.EndDate),
            services.Count,
            byCategory,
            byStatus,
            // Rule 3: the sum of the ACCEPTED proposals, in euros, with two decimal places.
            decimal.Round(services.Sum(s => s.AcceptedAmount), 2),
            completedHistory);
    }
}