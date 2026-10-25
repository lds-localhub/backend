using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// Represents the proposal of a provider for a service, with direct requests or published (F007, F011).
/// A proposal cannot be changed after it is submitted (RF-F007-01, rule 4).
/// </summary>
public class Proposal
{
    // Limits of the API documentation (E25), the same as the columns of the data model.
    public const int ConditionsMaxLength = 1000;
    public const int ObsMaxLength = 500;
    public const decimal AmountMax = 100000.00m;

    public const string AmountField = "amount";
    public const string ConditionsField = "conditions";
    public const string ObsField = "obs";

    public int ProposalId { get; private set; }
    public int ServiceId { get; private set; }
    public int ProfessionalProfileId { get; private set; }
    // "= null!": always set by Service.SubmitProposal or loaded by Entity Framework with Include.
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Conditions { get; private set; } = null!;
    public string? Obs { get; private set; }
    public ProposalStatus Status { get; private set; }
    // Set when the proposal is accepted, declined or voided; null while PENDING.
    public DateTimeOffset? DecisionDate { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Private: Entity Framework uses this constructor when it reads a proposal.
    private Proposal() { }

    // Only the Service creates proposals, so the rules of RF-F007-01 and RF-F011-02 are always applied.
    internal Proposal(ProfessionalProfile profile, decimal? amount, string? conditions, string? obs, DateTimeOffset now)
    {
        ProfessionalProfile = profile;
        ProfessionalProfileId = profile.ProfessionalProfileId;
        Amount = ValidateAmount(amount);
        Conditions = ValidateConditions(conditions);
        Obs = ValidateObs(obs);
        Status = ProposalStatus.Pending;
        CreationDate = now;
    }

    // Section 10.3: PENDING → ACCEPTED, when the client accepts the proposal (RF-F007-03, RF-F008-02).
    internal void Accept(DateTimeOffset now) => Decide(ProposalStatus.Accepted, now);

    // Section 10.3: PENDING → DECLINED, when the client declines it or accepts another one (RF-F007-04, RF-F008-03).
    internal void Decline(DateTimeOffset now) => Decide(ProposalStatus.Declined, now);

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

    // The validations below are public so that the service of the application can collect the errors of every
    // field in a single 400 response.

    // Greater than 0 and up to 100 000,00, with at most two decimal places (12.345 is rejected).
    public static decimal ValidateAmount(decimal? amount)
    {
        if (amount is null || amount.Value <= 0 || amount.Value > AmountMax || decimal.Round(amount.Value, 2) != amount.Value)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "O valor é obrigatório, maior que 0 e até 100 000,00 €, com no máximo duas casas decimais.", AmountField);
        }

        return amount.Value;
    }

    public static string ValidateConditions(string? conditions)
    {
        string trimmed = conditions?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > ConditionsMaxLength)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"As condições são obrigatórias e têm no máximo {ConditionsMaxLength} caracteres.", ConditionsField);
        }

        return trimmed;
    }

    // Optional: a missing or blank text is not saved.
    public static string? ValidateObs(string? obs)
    {
        string trimmed = obs?.Trim() ?? string.Empty;
        if (trimmed.Length > ObsMaxLength)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"As observações têm no máximo {ObsMaxLength} caracteres.", ObsField);
        }

        return trimmed.Length == 0 ? null : trimmed;
    }
}