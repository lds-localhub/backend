using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

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
    // The service must be loaded with its client, category, direct requests, proposals (with profile and provider),
    // review and history (with the user of each change).
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

public record ProviderSummary(int ProfessionalProfileId, string Name, decimal? AverageRating, int ReviewCount)
{
    // The profile must be loaded with its provider.
    public static ProviderSummary From(ProfessionalProfile profile, IReadOnlyDictionary<int, int> reviewCounts)
    {
        int reviewCount = reviewCounts.GetValueOrDefault(profile.ProfessionalProfileId);
        return new ProviderSummary(
            profile.ProfessionalProfileId, profile.ServiceProvider.Name, profile.AverageRating, reviewCount);
    }
}

public record ClientDirectRequestResponse(
    int DirectRequestId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    ProviderSummary ProfessionalProfile,
    ProposalResponse? Proposal)
{
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

public record AssignedProvider(int ProfessionalProfileId, string Name);