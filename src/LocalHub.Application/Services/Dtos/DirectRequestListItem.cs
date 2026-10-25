using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Item of the list of E23, as the provider sees it: the request, the summary of the service and the proposal of
// the provider, when there is one. It has no data of the Client.
public record DirectRequestListItem(
    int DirectRequestId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate,
    DirectRequestServiceSummary Service,
    DirectRequestProposalSummary? Proposal);

public record DirectRequestServiceSummary(
    int ServiceId,
    ServiceCategorySummary ServiceCategory,
    string Description,
    string Location,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status);

public record DirectRequestProposalSummary(
    int ProposalId,
    decimal Amount,
    string Conditions,
    string? Obs,
    ProposalStatus Status);

// Direct request as returned by E24.
public record DirectRequestStatusResponse(
    int DirectRequestId,
    int ServiceId,
    int ProfessionalProfileId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate)
{
    public static DirectRequestStatusResponse From(int serviceId, DirectRequest request)
    {
        return new DirectRequestStatusResponse(
            request.DirectRequestId,
            serviceId,
            request.ProfessionalProfileId,
            request.Status,
            request.CreationDate,
            request.ResponseDate);
    }
}