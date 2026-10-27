using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Proposal as returned by E25 and, inside other responses, by E22 and E23 (API documentation, E25).
public record ProposalResponse(
    int ProposalId,
    int ServiceId,
    int ProfessionalProfileId,
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
            proposal.ServiceId,
            proposal.ProfessionalProfileId,
            proposal.Amount,
            proposal.Conditions,
            proposal.Obs,
            proposal.Status,
            proposal.CreationDate,
            proposal.DecisionDate);
    }
}

// The "myProposal" of E22: the proposal without the ids of the service and of the profile (API documentation, E22).
public record MyProposalResponse(
    int ProposalId,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate)
{
    public static MyProposalResponse From(Proposal proposal)
    {
        return new MyProposalResponse(
            proposal.ProposalId,
            proposal.Amount,
            proposal.Conditions,
            proposal.Obs,
            proposal.Status,
            proposal.CreationDate,
            proposal.DecisionDate);
    }
}

// Summary of the service in E27.
public record ProposalServiceSummary(
    int ServiceId,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    string Title,
    string Description,
    string Location,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status);

// Item of the list of E27: the proposal and the summary of its service.
public record MyProposalListItem(
    int ProposalId,
    int ServiceId,
    int ProfessionalProfileId,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? DecisionDate,
    ProposalServiceSummary Service);