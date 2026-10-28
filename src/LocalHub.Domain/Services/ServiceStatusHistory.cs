using LocalHub.Domain.Accounts;

namespace LocalHub.Domain.Services;

/// <summary>
/// Represents a change of state of a service, with the date and the user that made it (Requirements Specification
/// v04, sections 10.1 and 10.2).
/// </summary>
public class ServiceStatusHistory
{
    public int ServiceStatusHistoryId { get; private set; }
    public int ServiceId { get; private set; }
    public ServiceStatus PreviousStatus { get; private set; }
    public ServiceStatus NewStatus { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public int ChangedByUserId { get; private set; }

    // Navigations (they are not columns). "= null!": always set by the Service or loaded by Entity Framework with
    // Include. The name ChangedBy lets Entity Framework find the foreign key ChangedByUserId by convention.
    public Service Service { get; private set; } = null!;
    public User ChangedBy { get; private set; } = null!;

    // Private: Entity Framework uses this constructor when it reads a change of state.
    private ServiceStatusHistory() { }

    // Only the Service records its changes of state, so the transitions of section 10 are always applied.
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