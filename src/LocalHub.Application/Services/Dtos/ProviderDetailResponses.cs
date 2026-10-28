using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// E36 (RF-F007-09): a direct request as the provider that received it sees it, with the proposal that the provider
// presented in it (consulted in E37), if any. It has no requests nor proposals of other providers (RNF-06).
public record ProviderDirectRequestResponse(
    int DirectRequestId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    DirectRequestServiceDetail Service,
    int? ProposalId);

// The service of E36: only the name of the Client, without any contacts. distanceKm is null while the provider has no
// registered location.
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

// Change of state of the service in E36: without the id nor the name of the author, so the hired provider is not
// identified (API documentation, E36).
public record ProviderServiceHistoryResponse(
    ServiceStatus PreviousStatus,
    ServiceStatus NewStatus,
    DateTimeOffset ChangedAt,
    UserRole ChangedByRole);

// E37 (RF-F008-07): a proposal as the provider that presented it sees it.
public record ProviderProposalResponse(
    int ProposalId,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate,
    ProposalServiceDetail Service);

// The service of E37: only the name of the Client, without any contacts.
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