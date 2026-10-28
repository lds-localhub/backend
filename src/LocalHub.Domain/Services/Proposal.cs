using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// Represents the proposal of a provider for a service, with direct requests or published (F007, F011).
/// A proposal cannot be changed after it is submitted (RF-F007-01, rule 4).
/// </summary>
public class Proposal
{
    // Limits of the API documentation (E25), the same as the columns of the data model. The DTOs use the same
    // constants (rule R17).
    public const int ConditionsMaxLength = 1000;
    public const int ObsMaxLength = 500;
    public const double AmountMin = 0.01;
    public const double AmountMax = 100000.00;

    public int ProposalId { get; private set; }
    public int ServiceId { get; private set; }
    public int ProfessionalProfileId { get; private set; }
    public string Conditions { get; private set; } = null!;
    public string? Obs { get; private set; }
    public decimal Amount { get; private set; }
    public ProposalStatus Status { get; private set; }
    // Set when the proposal is accepted, declined or voided; null while PENDING.
    public DateTimeOffset? DecisionDate { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Navigations (they are not columns). "= null!": always set by Service.SubmitProposal or loaded by Entity
    // Framework with Include.
    public Service Service { get; private set; } = null!;
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;

    // Private: Entity Framework uses this constructor when it reads a proposal.
    private Proposal() { }

    // Only the Service creates proposals, so the rules of RF-F007-01 and RF-F011-02 are always applied.
    // The fields were validated by the DTO; a blank obs arrives as null (rule R17).
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

    // Section 10.3: PENDING → ACCEPTED, when the client accepts the proposal (RF-F007-03, RF-F008-02).
    internal void Accept(DateTimeOffset now) => Decide(ProposalStatus.Accepted, now);

    // Section 10.3: PENDING → DECLINED, when the client declines it or accepts another one (RF-F007-04, RF-F008-03).
    internal void Decline(DateTimeOffset now) => Decide(ProposalStatus.Declined, now);

    // Section 10.3: PENDING → VOID, when the client cancels an OPEN service (RF-F007-05, RF-F008-04).
    internal void Void(DateTimeOffset now) => Decide(ProposalStatus.Void, now);

    // The other states are final (RF-F008-06).
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