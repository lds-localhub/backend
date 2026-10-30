namespace LocalHub.Application.Reports.Dtos;

// E34 (RF-F014-01, RF-F014-02): the activity report of the provider (API documentation v04, E34).
public record ProviderReportResponse(
    int CompletedServices,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<StatusCount> ByStatus,
    int DirectRequestsReceived,
    ProposalCounts Proposals,
    decimal TotalAcceptedAmount,
    IReadOnlyList<MonthlyRating> RatingEvolution);

public record ProposalCounts(int Accepted, int Declined);

// AverageRating is null in a month without reviews.
public record MonthlyRating(string Month, int ReviewCount, decimal? AverageRating);