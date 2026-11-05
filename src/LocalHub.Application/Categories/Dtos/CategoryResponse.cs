using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;

namespace LocalHub.Application.Categories.Dtos;

public record CategoryResponse(int ServiceCategoryId, string Name, ActivationStatus Status)
{
    public static CategoryResponse From(ServiceCategory category)
    {
        return new CategoryResponse(category.ServiceCategoryId, category.Name, category.Status);
    }
}