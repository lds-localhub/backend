using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

public record DirectRequestListItem(
    int DirectRequestId,
    DirectRequestStatus Status,
    int? ProposalId,
    DirectRequestServiceSummary Service);

public record DirectRequestServiceSummary(
    int ServiceId,
    string Title,
    ServiceCategorySummary ServiceCategory,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    string ClientName);

public record DirectRequestStatusResponse(int DirectRequestId, DirectRequestStatus Status, DateTimeOffset? ResponseDate)
{
    public static DirectRequestStatusResponse From(DirectRequest request)
    {
        return new DirectRequestStatusResponse(request.DirectRequestId, request.Status, request.ResponseDate);
    }
}