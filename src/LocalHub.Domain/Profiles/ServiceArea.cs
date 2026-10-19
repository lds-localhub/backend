using LocalHub.Domain.Categories;

namespace LocalHub.Domain.Profiles;

/// <summary>
/// Represents a category of services in which a provider works (table "ServiceArea" of the data model).
/// </summary>
public class ServiceArea
{
    public int ServiceCategoryId { get; private set; }
    public int ProfessionalProfileId { get; private set; }
    // "= null!": always set by ProfessionalProfile or loaded by Entity Framework with Include.
    public ServiceCategory ServiceCategory { get; private set; } = null!;

    // Private: Entity Framework uses this constructor when it reads an area.
    private ServiceArea() { }

    // Only the ProfessionalProfile creates areas, so the category rules (RF-F004-04) are always applied.
    internal ServiceArea(ServiceCategory category)
    {
        ServiceCategory = category;
        ServiceCategoryId = category.ServiceCategoryId;
    }
}