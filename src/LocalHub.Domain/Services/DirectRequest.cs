using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// Represents a request for a proposal sent to a provider chosen by the client (F009).
/// </summary>
public class DirectRequest
{
    public int DirectRequestId { get; private set; }
    public int ServiceId { get; private set; }
    public int ProfessionalProfileId { get; private set; }
    public DirectRequestStatus Status { get; private set; }
    // Set when the provider answers (proposal submitted or declined); null while PENDING and when VOID.
    public DateTimeOffset? ResponseDate { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Navigations (they are not columns). "= null!": always set by Service.Create or loaded by Entity Framework with
    // Include.
    public Service Service { get; private set; } = null!;
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;

    // Private: Entity Framework uses this constructor when it reads a direct request.
    private DirectRequest() { }

    // Only the Service creates direct requests, so the rules of RF-F009-01 are always applied.
    internal DirectRequest(ProfessionalProfile profile, DateTimeOffset now)
    {
        ProfessionalProfile = profile;
        ProfessionalProfileId = profile.ProfessionalProfileId;
        Status = DirectRequestStatus.Pending;
        CreationDate = now;
    }

    // Section 10.4: PENDING → PROPOSAL_SUBMITTED, when the provider submits a proposal (RF-F007-01).
    internal void MarkProposalSubmitted(DateTimeOffset now) => Answer(DirectRequestStatus.ProposalSubmitted, now);

    // Section 10.4: PENDING → DECLINED, when the provider declines the request (RF-F007-02).
    internal void Decline(DateTimeOffset now) => Answer(DirectRequestStatus.Declined, now);

    // The other states are final (RF-F007-08).
    private void Answer(DirectRequestStatus newStatus, DateTimeOffset now)
    {
        if (Status != DirectRequestStatus.Pending)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "A solicitação direta já não está pendente.");
        }

        Status = newStatus;
        ResponseDate = now;
    }
}