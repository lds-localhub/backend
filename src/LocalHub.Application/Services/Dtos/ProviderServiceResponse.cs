using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Service as the provider sees it in E22: without clientId and contacts of the Client, and with only the own direct
// request and proposal (RNF-06). The history has only the role of the author of each change, as in E36, so the hired
// provider is not identified. distanceKm is null while the provider has no registered location.
public record ProviderServiceResponse(
    int ServiceId,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    string Title,
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
    IReadOnlyList<ProviderServiceHistoryResponse> History);

// Change of state of the service (ServiceStatusHistory), with the user that made it, so that the detail shows, for
// example, who cancelled the service (API documentation, E22).
public record ServiceHistoryResponse(
    ServiceStatus PreviousStatus,
    ServiceStatus NewStatus,
    DateTimeOffset ChangedAt,
    int ChangedByUserId,
    string ChangedByName,
    UserRole ChangedByRole)
{
    // The record must be loaded with the user that made the change.
    public static ServiceHistoryResponse From(ServiceStatusHistory record)
    {
        return new ServiceHistoryResponse(
            record.PreviousStatus,
            record.NewStatus,
            record.ChangedAt,
            record.ChangedByUserId,
            record.ChangedBy.Name,
            record.ChangedBy.Role);
    }
}