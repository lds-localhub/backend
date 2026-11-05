using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

public record ProviderDirectRequestResponse(
    int DirectRequestId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    DirectRequestServiceDetail Service,
    int? ProposalId);

public record DirectRequestServiceDetail(
    int ServiceId,
    ServiceCategorySummary ServiceCategory,
    string Title,
    string Description,
    string Location,
    decimal? DistanceKm,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    DateTimeOffset CreationDate,
    string ClientName,
    IReadOnlyList<ProviderServiceHistoryResponse> History);

public record ProviderServiceHistoryResponse(
    ServiceStatus PreviousStatus,
    ServiceStatus NewStatus,
    DateTimeOffset ChangedAt,
    UserRole ChangedByRole);

public record ProviderProposalResponse(
    int ProposalId,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate,
    ProposalServiceDetail Service);

public record ProposalServiceDetail(
    int ServiceId,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    string Title,
    string Description,
    string Location,
    decimal? DistanceKm,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    DateTimeOffset CreationDate,
    string ClientName);