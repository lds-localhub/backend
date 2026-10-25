using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Service as the provider sees it in E22: without clientId and contacts of the Client, and with only the own direct
// request and proposal (RNF-06). distanceKm is null while the provider has no registered location.
public record ProviderServiceResponse(
    int ServiceId,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    string Description,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    string Location,
    decimal Latitude,
    decimal Longitude,
    decimal? DistanceKm,
    DateTimeOffset CreationDate,
    DateTimeOffset? ActualCompletionDate,
    bool Reviewed,
    IReadOnlyList<DirectRequestResponse> DirectRequests,
    MyProposalResponse? MyProposal,
    MyProposalResponse? AcceptedProposal,
    IReadOnlyList<ServiceHistoryResponse> History);

// Change of state of the service (ServiceStatusHistory).
public record ServiceHistoryResponse(
    ServiceStatus PreviousStatus,
    ServiceStatus NewStatus,
    DateTimeOffset ChangedAt,
    int ChangedByUserId);