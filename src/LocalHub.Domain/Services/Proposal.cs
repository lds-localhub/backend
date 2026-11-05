using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// The proposal of a provider for a service. It cannot be changed after it is submitted.
/// </summary>
public class Proposal
{
    public const int ConditionsMaxLength = 1000;
    public const int ObsMaxLength = 500;
    public const double AmountMin = 0.01;
    public const double AmountMax = 100000.00;

    public const string InactiveProviderErrorType = "prestador-inativo";

    public int ProposalId { get; private set; }
    public int ServiceId { get; private set; }
    public int ProfessionalProfileId { get; private set; }
    public string Conditions { get; private set; } = null!;
    public string? Obs { get; private set; }
    public decimal Amount { get; private set; }
    public ProposalStatus Status { get; private set; }
    // Null until the client decides.
    public DateTimeOffset? DecisionDate { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Navigations: set by Service.SubmitProposal or loaded by Entity Framework with Include.
    public Service Service { get; private set; } = null!;
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;

    // Used by Entity Framework; a proposal is created by Service.SubmitProposal.
    private Proposal() { }

    internal Proposal(ProfessionalProfile profile, decimal amount, string conditions, string? obs, DateTimeOffset now)
    {
        ProfessionalProfile = profile;
        ProfessionalProfileId = profile.ProfessionalProfileId;
        Amount = amount;
        Conditions = conditions.Trim();
        Obs = obs?.Trim();
        Status = ProposalStatus.Pending;
        CreationDate = now;
    }

    internal void Accept(DateTimeOffset now) => Decide(ProposalStatus.Accepted, now);

    internal void Decline(DateTimeOffset now) => Decide(ProposalStatus.Declined, now);

    internal void Void()
    {
        if (Status != ProposalStatus.Pending)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "A proposta já não está pendente.");
        }

        Status = ProposalStatus.Void;
    }

    // Any other state is final.
    private void Decide(ProposalStatus newStatus, DateTimeOffset now)
    {
        if (Status != ProposalStatus.Pending)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "A proposta já não está pendente.");
        }

        Status = newStatus;
        DecisionDate = now;
    }
}