using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;

namespace LocalHub.Application.Categories.Dtos;

// Category as returned by E08 to E11 (API documentation, section F003).
public record CategoryResponse(int ServiceCategoryId, string Name, ActivationStatus Status)
{
    public static CategoryResponse From(ServiceCategory category)
    {
        return new CategoryResponse(category.ServiceCategoryId, category.Name, category.Status);
    }
}