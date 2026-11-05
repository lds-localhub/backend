using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

public record ServiceStatusResponse(int ServiceId, ServiceStatus Status)
{
    public static ServiceStatusResponse From(Service service)
    {
        return new ServiceStatusResponse(service.ServiceId, service.Status);
    }
}

public record CompletedServiceResponse(int ServiceId, ServiceStatus Status, DateTimeOffset? ActualCompletionDate)
{
    public static CompletedServiceResponse From(Service service)
    {
        return new CompletedServiceResponse(service.ServiceId, service.Status, service.ActualCompletionDate);
    }
}

public record ServiceCategorySummary(int ServiceCategoryId, string Name);