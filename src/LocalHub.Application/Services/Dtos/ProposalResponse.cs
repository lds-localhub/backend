using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

public record ProposalResponse(
    int ProposalId,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate)
{
    public static ProposalResponse From(Proposal proposal)
    {
        return new ProposalResponse(
            proposal.ProposalId,
            proposal.Amount,
            proposal.Conditions,
            proposal.Obs,
            proposal.Status,
            proposal.CreationDate,
            proposal.DecisionDate);
    }
}

public record ProposalDecisionResponse(int ProposalId, ProposalStatus Status, DateTimeOffset? DecisionDate)
{
    public static ProposalDecisionResponse From(Proposal proposal)
    {
        return new ProposalDecisionResponse(proposal.ProposalId, proposal.Status, proposal.DecisionDate);
    }
}

public record ProposalServiceSummary(
    int ServiceId,
    string Title,
    ServiceCategorySummary ServiceCategory,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    string ClientName);

public record MyProposalListItem(int ProposalId, decimal Amount, ProposalStatus Status, ProposalServiceSummary Service);