using LocalHub.Application.Reports.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports;

public interface IContractedService
{
    int ServiceCategoryId { get; }
    string CategoryName { get; }
    ServiceStatus Status { get; }
    decimal AcceptedAmount { get; }
}

public static class ContractedIndicators
{
    private static readonly ServiceStatus[] ContractedStates =
    {
        ServiceStatus.Assigned,
        ServiceStatus.Completed,
        ServiceStatus.Cancelled,
    };

    public static IReadOnlyList<CategoryCount> ByCategory(IEnumerable<IContractedService> services)
    {
        return services
            .GroupBy(s => new { s.ServiceCategoryId, s.CategoryName })
            .Select(g => new CategoryCount(g.Key.ServiceCategoryId, g.Key.CategoryName, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }

    // Always the three states, also with 0.
    public static IReadOnlyList<StatusCount> ByStatus(IReadOnlyCollection<IContractedService> services)
    {
        return ContractedStates
            .Select(state => new StatusCount(state, services.Count(s => s.Status == state)))
            .ToList();
    }

    public static decimal TotalAcceptedAmount(IEnumerable<IContractedService> services)
    {
        return decimal.Round(services.Sum(s => s.AcceptedAmount), 2);
    }
}