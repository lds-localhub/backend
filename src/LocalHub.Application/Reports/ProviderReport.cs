using System.Globalization;

using LocalHub.Application.Reports.Dtos;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Reports;

public record ProviderContractedService(
    int ServiceCategoryId,
    string CategoryName,
    ServiceStatus Status,
    decimal AcceptedAmount) : IContractedService;

public record ReceivedReview(DateTimeOffset CreationDate, short Rating);

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
            new ProposalCounts(services.Count, declinedProposals),
            ContractedIndicators.TotalAcceptedAmount(services),
            BuildRatingEvolution(reviews, period, today));
    }

    // From the month of the first review to the current month, limited to the period.
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
        if (period.StartDate != null && FirstDayOfMonth(period.StartDate.Value) > first)
        {
            first = FirstDayOfMonth(period.StartDate.Value);
        }

        var last = FirstDayOfMonth(today);
        if (period.EndDate != null && FirstDayOfMonth(period.EndDate.Value) < last)
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