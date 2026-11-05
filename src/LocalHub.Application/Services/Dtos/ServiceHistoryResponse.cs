using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

public record ServiceHistoryResponse(
    ServiceStatus PreviousStatus,
    ServiceStatus NewStatus,
    DateTimeOffset ChangedAt,
    int ChangedByUserId,
    string ChangedByName,
    UserRole ChangedByRole)
{
    // The record must be loaded with the user that made the change.
    public static ServiceHistoryResponse From(ServiceStatusHistory record)
    {
        return new ServiceHistoryResponse(
            record.PreviousStatus,
            record.NewStatus,
            record.ChangedAt,
            record.ChangedByUserId,
            record.ChangedBy.Name,
            record.ChangedBy.Role);
    }
}