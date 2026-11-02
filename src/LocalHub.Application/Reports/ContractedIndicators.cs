using LocalHub.Application.Reports.Dtos;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports;

// A service with an ACCEPTED proposal (it reached ASSIGNED), with the data that the reports of both roles need.
public interface IContractedService
{
    int ServiceCategoryId { get; }
    string CategoryName { get; }
    ServiceStatus Status { get; }
    decimal AcceptedAmount { get; }
}

// The indicators that E33 and E34 calculate in the same way, from the contracted services already filtered by user and
// period (API documentation v04).
public static class ContractedIndicators
{
    // The states that a contracted service can have, in the order of the life cycle (section 10).
    private static readonly ServiceStatus[] ContractedStates =
    {
        ServiceStatus.Assigned,
        ServiceStatus.Completed,
        ServiceStatus.Cancelled,
    };

    // One line per category with contracted services, the most frequent first.
    public static IReadOnlyList<CategoryCount> ByCategory(IEnumerable<IContractedService> services)
    {
        return services
            .GroupBy(s => new { s.ServiceCategoryId, s.CategoryName })
            .Select(g => new CategoryCount(g.Key.ServiceCategoryId, g.Key.CategoryName, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }

    // Always the three states, also with 0, so that the user sees the whole distribution.
    public static IReadOnlyList<StatusCount> ByStatus(IReadOnlyCollection<IContractedService> services)
    {
        return ContractedStates
            .Select(state => new StatusCount(state, services.Count(s => s.Status == state)))
            .ToList();
    }

    // The sum of the ACCEPTED proposals, in euros, with two decimal places.
    public static decimal TotalAcceptedAmount(IEnumerable<IContractedService> services)
    {
        return decimal.Round(services.Sum(s => s.AcceptedAmount), 2);
    }
}