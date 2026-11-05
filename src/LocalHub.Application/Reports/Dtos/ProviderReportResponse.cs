namespace LocalHub.Application.Reports.Dtos;

public record ProviderReportResponse(
    int CompletedServices,
    IReadOnlyList<CategoryCount> ByCategory,
    IReadOnlyList<StatusCount> ByStatus,
    int DirectRequestsReceived,
    ProposalCounts Proposals,
    decimal TotalAcceptedAmount,
    IReadOnlyList<MonthlyRating> RatingEvolution);

public record ProposalCounts(int Accepted, int Declined);

public record MonthlyRating(string Month, int ReviewCount, decimal? AverageRating);