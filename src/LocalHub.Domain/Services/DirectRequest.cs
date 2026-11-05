using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.Domain.Services;

/// <summary>
/// A request for a proposal sent to a provider chosen by the client.
/// </summary>
public class DirectRequest
{
    public int DirectRequestId { get; private set; }
    public int ServiceId { get; private set; }
    public int ProfessionalProfileId { get; private set; }
    public DirectRequestStatus Status { get; private set; }
    // Null until the provider answers.
    public DateTimeOffset? ResponseDate { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Navigations: set by Service.Create or loaded by Entity Framework with Include.
    public Service Service { get; private set; } = null!;
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;

    // Used by Entity Framework; a direct request is created by Service.Create.
    private DirectRequest() { }

    internal DirectRequest(ProfessionalProfile profile, DateTimeOffset now)
    {
        ProfessionalProfile = profile;
        ProfessionalProfileId = profile.ProfessionalProfileId;
        Status = DirectRequestStatus.Pending;
        CreationDate = now;
    }

    internal void MarkProposalSubmitted(DateTimeOffset now) => Answer(DirectRequestStatus.ProposalSubmitted, now);

    internal void Decline(DateTimeOffset now) => Answer(DirectRequestStatus.Declined, now);

    internal void Void()
    {
        if (Status != DirectRequestStatus.Pending)
        {
            throw new DomainException(DomainErrorKind.InvalidTransition, "A solicitação direta já não está pendente.");
        }

        Status = DirectRequestStatus.Void;
    }

    // Any other state is final.
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