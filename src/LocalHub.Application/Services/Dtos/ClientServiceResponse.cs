using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Service as the client that created it sees it in E22 (and in the response of E20): all the direct requests, each with
// its proposal, and, in PUBLIC, all the proposals received (API documentation, E22). The list of the other model is
// empty.
public record ClientServiceResponse(
    int ServiceId,
    string Title,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    string Description,
    string Location,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ActualCompletionDate,
    string ClientName,
    bool Reviewed,
    IReadOnlyList<ServiceHistoryResponse> History,
    IReadOnlyList<ClientDirectRequestResponse> DirectRequests,
    IReadOnlyList<ClientProposalResponse> Proposals)
{
    // The service must be loaded with its client, its category, its direct requests and its proposals, each with the
    // profile and the provider, its review (reviewed) and the history of states, each change with the user that made
    // it. reviewCounts has the number of reviews of each provider (a provider without reviews is not there). The
    // history is in chronological order; the proposals of PUBLIC, newest first.
    public static ClientServiceResponse From(Service service, IReadOnlyDictionary<int, int> reviewCounts)
    {
        var directRequests = new List<ClientDirectRequestResponse>();
        var proposals = new List<ClientProposalResponse>();
        if (service.ProposalModel == ProposalModel.DirectRequest)
        {
            directRequests = service.DirectRequests
                .OrderBy(r => r.DirectRequestId)
                .Select(r => ClientDirectRequestResponse.From(r, service.Proposals, reviewCounts))
                .ToList();
        }
        else
        {
            proposals = service.Proposals
                .OrderByDescending(p => p.CreationDate)
                .ThenByDescending(p => p.ProposalId)
                .Select(p => ClientProposalResponse.From(p, reviewCounts))
                .ToList();
        }

        return new ClientServiceResponse(
            service.ServiceId,
            service.Title,
            service.ProposalModel,
            new ServiceCategorySummary(service.ServiceCategoryId, service.ServiceCategory.Name),
            service.Description,
            service.Location,
            service.RequestedStartDate,
            service.RequestedEndDate,
            service.Status,
            service.CreationDate,
            service.ActualCompletionDate,
            service.Client.Name,
            Reviewed: service.Review != null,
            service.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .ThenBy(h => h.ServiceStatusHistoryId)
                .Select(ServiceHistoryResponse.From)
                .ToList(),
            directRequests,
            proposals);
    }
}

// Summary of the provider shown to the client in E22.
public record ProviderSummary(int ProfessionalProfileId, string Name, decimal? AverageRating, int ReviewCount)
{
    // The profile must be loaded with its provider.
    public static ProviderSummary From(ProfessionalProfile profile, IReadOnlyDictionary<int, int> reviewCounts)
    {
        reviewCounts.TryGetValue(profile.ProfessionalProfileId, out int reviewCount);
        return new ProviderSummary(
            profile.ProfessionalProfileId, profile.ServiceProvider.Name, profile.AverageRating, reviewCount);
    }
}

// Direct request of E22 for the client: the provider and the proposal it submitted, or null.
public record ClientDirectRequestResponse(
    int DirectRequestId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    ProviderSummary ProfessionalProfile,
    ProposalResponse? Proposal)
{
    // proposals are the proposals of the service; the one of the request is the one of the same profile.
    public static ClientDirectRequestResponse From(
        DirectRequest request, IEnumerable<Proposal> proposals, IReadOnlyDictionary<int, int> reviewCounts)
    {
        var proposal = proposals.SingleOrDefault(p => p.ProfessionalProfileId == request.ProfessionalProfileId);
        return new ClientDirectRequestResponse(
            request.DirectRequestId,
            request.Status,
            request.CreationDate,
            request.ResponseDate,
            ProviderSummary.From(request.ProfessionalProfile, reviewCounts),
            proposal == null ? null : ProposalResponse.From(proposal));
    }
}

// Proposal of PUBLIC as the client sees it in E22: the proposal with the summary of the provider.
public record ClientProposalResponse(
    int ProposalId,
    ProviderSummary ProfessionalProfile,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate)
{
    public static ClientProposalResponse From(Proposal proposal, IReadOnlyDictionary<int, int> reviewCounts)
    {
        return new ClientProposalResponse(
            proposal.ProposalId,
            ProviderSummary.From(proposal.ProfessionalProfile, reviewCounts),
            proposal.Amount,
            proposal.Conditions,
            proposal.Obs,
            proposal.Status,
            proposal.CreationDate,
            proposal.DecisionDate);
    }
}

// Item of the list of E21: the summary of the service that the lists of the client show (API documentation, E21).
// directRequestsCount and proposalsCount count the direct requests and the proposals in any state.
public record ServiceListItem(
    int ServiceId,
    string Title,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    ServiceStatus Status,
    DateTimeOffset CreationDate,
    int DirectRequestsCount,
    int ProposalsCount,
    int PendingProposalsCount,
    AssignedProvider? AssignedProvider,
    bool Reviewed);

// The provider of the accepted proposal, in E21; null while no proposal was accepted.
public record AssignedProvider(int ProfessionalProfileId, string Name);