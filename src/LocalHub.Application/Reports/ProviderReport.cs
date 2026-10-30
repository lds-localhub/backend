using System.Globalization;

using LocalHub.Application.Reports.Dtos;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports;

// A service in which the proposal of the provider was accepted, with the data of the report.
public record ProviderContractedService(
    int ServiceCategoryId,
    string CategoryName,
    ServiceStatus Status,
    decimal AcceptedAmount) : IContractedService;

// A review received by the provider.
public record ReceivedReview(DateTimeOffset CreationDate, short Rating);

// The indicators of E34 (RF-F014-01; API documentation v04). The services, the direct requests and the declined
// proposals arrive already filtered by provider and period; the reviews are all those of the provider, because the
// evolution depends on the month of the review and not on the creation date of the service.
public static class ProviderReport
{
    private const string MonthFormat = "yyyy-MM";

    public static ProviderReportResponse Build(
        IReadOnlyCollection<ProviderContractedService> services,
        int directRequestsReceived,
        int declinedProposals,
        IReadOnlyCollection<ReceivedReview> reviews,
        ReportPeriod period,
        DateOnly today)
    {
        return new ProviderReportResponse(
            services.Count(s => s.Status == ServiceStatus.Completed),
            ContractedIndicators.ByCategory(services),
            ContractedIndicators.ByStatus(services),
            directRequestsReceived,
            // A service has at most one ACCEPTED proposal, so each contracted service is one accepted proposal.
            new ProposalCounts(services.Count, declinedProposals),
            ContractedIndicators.TotalAcceptedAmount(services),
            BuildRatingEvolution(reviews, period, today));
    }

    // Rule 5: from the month of the first review to the current month, in UTC, limited to the months of the period when
    // there is one (RF-F014-02, rule 3). The limits also keep the list short with very distant dates in the period.
    private static IReadOnlyList<MonthlyRating> BuildRatingEvolution(
        IReadOnlyCollection<ReceivedReview> reviews, ReportPeriod period, DateOnly today)
    {
        if (reviews.Count == 0)
        {
            return Array.Empty<MonthlyRating>();
        }

        var ratingsByMonth = reviews.ToLookup(
            r => FirstDayOfMonth(DateOnly.FromDateTime(r.CreationDate.UtcDateTime)), r => r.Rating);

        var first = ratingsByMonth.Min(month => month.Key);
        if (period.StartDate is not null && FirstDayOfMonth(period.StartDate.Value) > first)
        {
            first = FirstDayOfMonth(period.StartDate.Value);
        }

        var last = FirstDayOfMonth(today);
        if (period.EndDate is not null && FirstDayOfMonth(period.EndDate.Value) < last)
        {
            last = FirstDayOfMonth(period.EndDate.Value);
        }

        var evolution = new List<MonthlyRating>();
        for (var month = first; month <= last; month = month.AddMonths(1))
        {
            var ratings = ratingsByMonth[month].ToList();
            evolution.Add(new MonthlyRating(
                month.ToString(MonthFormat, CultureInfo.InvariantCulture),
                ratings.Count,
                ProfessionalProfile.CalculateAverageRating(ratings)));
        }

        return evolution;
    }

    private static DateOnly FirstDayOfMonth(DateOnly date)
    {
        return new DateOnly(date.Year, date.Month, 1);
    }
}