using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Service as the client that created it sees it in E22 and in the response of E28: all the direct requests and the
// accepted proposal, each with the summary of the provider (API documentation, E22 and E28).
public record ClientServiceResponse(
    int ServiceId,
    int ClientId,
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
    DateTimeOffset CreationDate,
    DateTimeOffset? ActualCompletionDate,
    bool Reviewed,
    IReadOnlyList<ClientDirectRequestResponse> DirectRequests,
    ClientProposalResponse? AcceptedProposal,
    IReadOnlyList<ServiceHistoryResponse> History)
{
    // The service must be loaded with its category, its direct requests and its proposals, each with the profile and
    // the provider, and with the history of states, each change with the user that made it. The history is in
    // chronological order. There are no reviews yet (I052), so reviewed is false.
    public static ClientServiceResponse From(Service service)
    {
        var accepted = service.Proposals.SingleOrDefault(p => p.Status == ProposalStatus.Accepted);

        return new ClientServiceResponse(
            service.ServiceId,
            service.ClientId,
            service.ProposalModel,
            new ServiceCategorySummary(service.ServiceCategoryId, service.ServiceCategory.Name),
            service.Title,
            service.Description,
            service.RequestedStartDate,
            service.RequestedEndDate,
            service.Status,
            service.Location,
            service.Latitude,
            service.Longitude,
            service.CreationDate,
            service.ActualCompletionDate,
            Reviewed: false,
            service.DirectRequests.OrderBy(r => r.DirectRequestId).Select(ClientDirectRequestResponse.From).ToList(),
            accepted is null ? null : ClientProposalResponse.From(accepted),
            service.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .ThenBy(h => h.ServiceStatusHistoryId)
                .Select(ServiceHistoryResponse.From)
                .ToList());
    }
}

// Summary of the provider shown to the client: E22, E23, E26 and E28.
public record ProviderSummary(int ProfessionalProfileId, string Name, decimal? AverageRating)
{
    // The profile must be loaded with its provider.
    public static ProviderSummary From(ProfessionalProfile profile)
    {
        return new ProviderSummary(profile.ProfessionalProfileId, profile.ServiceProvider.Name, profile.AverageRating);
    }
}

// Direct request of E22 for the client: the fields of E20 and the provider.
public record ClientDirectRequestResponse(
    int DirectRequestId,
    int ProfessionalProfileId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    ProviderSummary ProfessionalProfile)
{
    public static ClientDirectRequestResponse From(DirectRequest request)
    {
        return new ClientDirectRequestResponse(
            request.DirectRequestId,
            request.ProfessionalProfileId,
            request.Status,
            request.CreationDate,
            request.ResponseDate,
            ProviderSummary.From(request.ProfessionalProfile));
    }
}

// Proposal as the client sees it in E26 and E22: the structure of E25 with the summary of the provider.
public record ClientProposalResponse(
    int ProposalId,
    int ServiceId,
    ProviderSummary ProfessionalProfile,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate)
{
    public static ClientProposalResponse From(Proposal proposal)
    {
        return new ClientProposalResponse(
            proposal.ProposalId,
            proposal.ServiceId,
            ProviderSummary.From(proposal.ProfessionalProfile),
            proposal.Amount,
            proposal.Conditions,
            proposal.Obs,
            proposal.Status,
            proposal.CreationDate,
            proposal.DecisionDate);
    }
}

// Item of the list of E21: the fields of E20 without directRequests, and the number of PENDING proposals (always 0
// for the provider).
public record ServiceListItem(
    int ServiceId,
    int ClientId,
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
    DateTimeOffset CreationDate,
    DateTimeOffset? ActualCompletionDate,
    int PendingProposalsCount);

// Item of the list of E23 for the client: the request, the provider, the summary of the service and the proposal of
// that provider, when there is one, so that the client can accept or decline it (E28, E29).
public record ClientDirectRequestListItem(
    int DirectRequestId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    ProviderSummary ProfessionalProfile,
    DirectRequestServiceSummary Service,
    DirectRequestProposalSummary? Proposal);