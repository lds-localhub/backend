using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Response of E30: the service and its new state (API documentation, E30).
public record ServiceStatusResponse(int ServiceId, ServiceStatus Status)
{
    public static ServiceStatusResponse From(Service service)
    {
        return new ServiceStatusResponse(service.ServiceId, service.Status);
    }
}

// Response of E31: the service, its new state and the completion date (API documentation, E31).
public record CompletedServiceResponse(int ServiceId, ServiceStatus Status, DateTimeOffset? ActualCompletionDate)
{
    public static CompletedServiceResponse From(Service service)
    {
        return new CompletedServiceResponse(service.ServiceId, service.Status, service.ActualCompletionDate);
    }
}

public record ServiceCategorySummary(int ServiceCategoryId, string Name);