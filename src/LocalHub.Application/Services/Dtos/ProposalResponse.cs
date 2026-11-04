using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Proposal as returned by E25 and, inside the direct requests, by E22 (API documentation, E25 and E22).
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

// Response of E28 and E29: only what changed in the proposal (API documentation, E28 and E29).
public record ProposalDecisionResponse(int ProposalId, ProposalStatus Status, DateTimeOffset? DecisionDate)
{
    public static ProposalDecisionResponse From(Proposal proposal)
    {
        return new ProposalDecisionResponse(proposal.ProposalId, proposal.Status, proposal.DecisionDate);
    }
}

// Summary of the service in E27: of the Client, only the name, without any contacts (RNF-06).
public record ProposalServiceSummary(
    int ServiceId,
    string Title,
    ServiceCategorySummary ServiceCategory,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    string ClientName);

// Item of the list of E27: the proposal and the summary of its service (API documentation, E27). The detail is
// consulted in E37.
public record MyProposalListItem(int ProposalId, decimal Amount, ProposalStatus Status, ProposalServiceSummary Service);