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
    // "= null!": always set by Service.Create or loaded by Entity Framework with Include.
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;
    public DirectRequestStatus Status { get; private set; }
    // Set when the provider answers (proposal submitted or declined); null while PENDING and when VOID.
    public DateTimeOffset? ResponseDate { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

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
}