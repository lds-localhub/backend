using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Item of the list of E23, as the provider sees it: the request, the proposal that the provider presented in it
// (consulted in E37, or null) and the summary of the service (API documentation, E23). The detail is consulted in E36.
public record DirectRequestListItem(
    int DirectRequestId,
    DirectRequestStatus Status,
    int? ProposalId,
    DirectRequestServiceSummary Service);

// The service of a request in E23: of the Client, only the name, without any contacts (RNF-06).
public record DirectRequestServiceSummary(
    int ServiceId,
    string Title,
    ServiceCategorySummary ServiceCategory,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    string ClientName);

// Response of E24: only what changed in the direct request (API documentation, E24).
public record DirectRequestStatusResponse(int DirectRequestId, DirectRequestStatus Status, DateTimeOffset? ResponseDate)
{
    public static DirectRequestStatusResponse From(DirectRequest request)
    {
        return new DirectRequestStatusResponse(request.DirectRequestId, request.Status, request.ResponseDate);
    }
}