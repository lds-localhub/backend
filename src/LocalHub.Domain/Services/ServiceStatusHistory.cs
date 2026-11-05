using LocalHub.Domain.Accounts;

namespace LocalHub.Domain.Services;

/// <summary>
/// A change of state of a service, with the date and the user that made it.
/// </summary>
public class ServiceStatusHistory
{
    public int ServiceStatusHistoryId { get; private set; }
    public int ServiceId { get; private set; }
    public ServiceStatus PreviousStatus { get; private set; }
    public ServiceStatus NewStatus { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public int ChangedByUserId { get; private set; }

    // Navigations: set by the Service or loaded by Entity Framework with Include.
    public Service Service { get; private set; } = null!;
    public User ChangedBy { get; private set; } = null!;

    // Used by Entity Framework; a change of state is recorded by the Service.
    private ServiceStatusHistory() { }

    internal ServiceStatusHistory(
        ServiceStatus previousStatus, ServiceStatus newStatus, User changedBy, DateTimeOffset now)
    {
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedBy = changedBy;
        ChangedByUserId = changedBy.UserId;
        ChangedAt = now;
    }
}