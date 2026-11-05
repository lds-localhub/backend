using LocalHub.Domain.Categories;

namespace LocalHub.Domain.Profiles;

/// <summary>
/// A category of services in which a provider works.
/// </summary>
public class ServiceArea
{
    public int ServiceCategoryId { get; private set; }
    public int ProfessionalProfileId { get; private set; }

    // Navigations: set by ProfessionalProfile or loaded by Entity Framework with Include.
    public ServiceCategory ServiceCategory { get; private set; } = null!;
    public ProfessionalProfile ProfessionalProfile { get; private set; } = null!;

    // Used by Entity Framework.
    private ServiceArea() { }

    internal ServiceArea(ServiceCategory category)
    {
        ServiceCategory = category;
        ServiceCategoryId = category.ServiceCategoryId;
    }
}