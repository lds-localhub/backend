using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Service as returned by E20, with the same structure in both modalities (API documentation, section F009 and F010).
// directRequests is always present: the direct requests created with DIRECT_REQUEST and [] with PUBLIC.
public record ServiceResponse(
    int ServiceId,
    int ClientId,
    ProposalModel ProposalModel,
    ServiceCategorySummary ServiceCategory,
    string Title,
    string Description,
    DateOnly RequestedStartDate,
    DateOnly? RequestedEndDate,
    ServiceStatus Status,
    string Location,
    decimal Latitude,
    decimal Longitude,
    DateTimeOffset CreationDate,
    DateTimeOffset? ActualCompletionDate,
    IReadOnlyList<DirectRequestResponse> DirectRequests)
{
    // The service must be loaded with its category and its direct requests.
    public static ServiceResponse From(Service service)
    {
        var directRequests = service.DirectRequests
            .OrderBy(r => r.DirectRequestId)
            .Select(DirectRequestResponse.From)
            .ToList();

        return new ServiceResponse(
            service.ServiceId,
            service.ClientId,
            service.ProposalModel,
            new ServiceCategorySummary(service.ServiceCategoryId, service.ServiceCategory.Name),
            service.Title,
            service.Description,
            service.RequestedStartDate,
            service.RequestedEndDate,
            service.Status,
            service.Location,
            service.Latitude,
            service.Longitude,
            service.CreationDate,
            service.ActualCompletionDate,
            directRequests);
    }
}

public record ServiceCategorySummary(int ServiceCategoryId, string Name);

public record DirectRequestResponse(
    int DirectRequestId,
    int ProfessionalProfileId,
    DirectRequestStatus Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? ResponseDate)
{
    public static DirectRequestResponse From(DirectRequest request)
    {
        return new DirectRequestResponse(
            request.DirectRequestId,
            request.ProfessionalProfileId,
            request.Status,
            request.CreationDate,
            request.ResponseDate);
    }
}