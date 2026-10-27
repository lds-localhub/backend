using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Domain.Categories;

/// <summary>
/// Represents a category of services (for example, plumbing), managed by the administrator (F003).
/// </summary>
public class ServiceCategory
{
    // Limit of the name (RF-F003-01, rule 1), the same as the column "name" varchar(60) of the data model.
    public const int NameMaxLength = 60;

    public int ServiceCategoryId { get; private set; }
    // "= null!" tells the compiler that the name is never null: it is always set by Create or by Entity Framework.
    public string Name { get; private set; } = null!;
    public ActivationStatus Status { get; private set; }

    // Navigations (they are not columns): the profiles and the services that use this category.
    private readonly List<ServiceArea> _serviceAreas = [];
    public IReadOnlyCollection<ServiceArea> ServiceAreas => _serviceAreas;
    private readonly List<Service> _services = [];
    public IReadOnlyCollection<Service> Services => _services;

    // Private: a category is created with Create. Entity Framework uses this constructor when it reads a category.
    private ServiceCategory() { }

    // RF-F003-01: the new category is always active. The name is validated by the DTO and its uniqueness depends on
    // the other categories, so it is checked by the CategoryService (rule R17).
    public static ServiceCategory Create(string name) => new()
    {
        Name = name.Trim(),
        Status = ActivationStatus.Active,
    };

    // RF-F003-02: only the name changes, so the associations to profiles and services are kept.
    public void Rename(string name)
    {
        Name = name.Trim();
    }

    // RF-F003-03: the category can no longer be associated to profiles or to new services.
    // The rule about providers and services that still use the category is checked by the CategoryService.
    public void Deactivate()
    {
        if (Status == ActivationStatus.Inactive)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A categoria já está desativada.");
        }

        Status = ActivationStatus.Inactive;
    }
}
