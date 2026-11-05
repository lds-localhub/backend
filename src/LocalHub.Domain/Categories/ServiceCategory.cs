using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.Domain.Categories;

/// <summary>
/// A category of services (for example, plumbing), managed by the administrator.
/// </summary>
public class ServiceCategory
{
    public const int NameMaxLength = 60;

    public int ServiceCategoryId { get; private set; }
    public string Name { get; private set; } = null!;
    public ActivationStatus Status { get; private set; }

    // Navigations: the profiles and the services that use this category.
    private readonly List<ServiceArea> _serviceAreas = new();
    public IReadOnlyCollection<ServiceArea> ServiceAreas => _serviceAreas;
    private readonly List<Service> _services = new();
    public IReadOnlyCollection<Service> Services => _services;

    // Used by Entity Framework; a category is created with Create.
    private ServiceCategory() { }

    public static ServiceCategory Create(string name) => new()
    {
        Name = name.Trim(),
        Status = ActivationStatus.Active,
    };

    public void Rename(string name)
    {
        Name = name.Trim();
    }

    public void Deactivate()
    {
        if (Status == ActivationStatus.Inactive)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A categoria já está desativada.");
        }

        Status = ActivationStatus.Inactive;
    }
}