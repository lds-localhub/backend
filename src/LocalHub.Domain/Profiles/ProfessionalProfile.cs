using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Services;

namespace LocalHub.Domain.Profiles;

/// <summary>
/// Represents the professional profile of a service provider (F004): presentation, contacts, experience,
/// categories in which the provider works and service radius.
/// </summary>
[Table("ProfessionalProfile")]
public class ProfessionalProfile
{
    // Limits of the API documentation (E12), the same as the columns of the data model. The DTOs use the same
    // constants (rule R17).
    public const int IntroductionMaxLength = 1000;
    public const int ContactDetailsMaxLength = 500;
    public const int ExperienceMaxLength = 2000;
    public const int ServiceRadiusMinKm = 1;
    public const int ServiceRadiusMaxKm = 100;

    public int ProfessionalProfileId { get; private set; }
    public int ServiceProviderId { get; private set; }
    [MaxLength(IntroductionMaxLength)] public string Introduction { get; private set; } = null!;
    [MaxLength(ContactDetailsMaxLength)] public string ContactDetails { get; private set; } = null!;
    [MaxLength(ExperienceMaxLength)] public string Experience { get; private set; } = null!;
    // Derived from the reviews (F012); null while the provider has no reviews. Never changed by the provider
    // (RF-F004-02, rule 3).
    [Column(TypeName = "numeric(2,1)")] public decimal? AverageRating { get; private set; }
    public short ServiceRadiusKm { get; private set; }

    // Navigations (they are not columns). "= null!": always set by Create or loaded by Entity Framework with Include.
    // The name ServiceProvider lets Entity Framework find the foreign key ServiceProviderId by convention.
    public User ServiceProvider { get; private set; } = null!;
    private readonly List<ServiceArea> _serviceAreas = [];
    public IReadOnlyCollection<ServiceArea> ServiceAreas => _serviceAreas;
    private readonly List<Proposal> _proposals = [];
    public IReadOnlyCollection<Proposal> Proposals => _proposals;
    private readonly List<DirectRequest> _directRequests = [];
    public IReadOnlyCollection<DirectRequest> DirectRequests => _directRequests;

    // Private: a profile is created with Create. Entity Framework uses this constructor when it reads a profile.
    private ProfessionalProfile() { }

    // RF-F004-01. The fields are validated by the DTO; "one profile per provider" and the categories depend on the
    // database, so they are checked by the ProfileService (rule R17).
    // The location of the provider is saved in User (E12 converts the address before calling Create).
    public static ProfessionalProfile Create(
        User provider,
        string introduction,
        string contactDetails,
        string experience,
        IReadOnlyCollection<ServiceCategory> categories,
        short serviceRadiusKm)
    {
        if (provider.Role != UserRole.Provider)
        {
            throw new DomainException(DomainErrorKind.Forbidden,
                "Só um Prestador de Serviços pode criar um perfil profissional.");
        }

        var profile = new ProfessionalProfile
        {
            ServiceProvider = provider,
            ServiceProviderId = provider.UserId,
            Introduction = introduction.Trim(),
            ContactDetails = contactDetails.Trim(),
            Experience = experience.Trim(),
            ServiceRadiusKm = serviceRadiusKm,
        };
        profile.ReplaceCategories(categories);

        return profile;
    }

    // RF-F004-02 (E14): the presentation, the contacts and the experience.
    public void UpdateDetails(string introduction, string contactDetails, string experience)
    {
        Introduction = introduction.Trim();
        ContactDetails = contactDetails.Trim();
        Experience = experience.Trim();
    }

    // RF-F004-04 (E12 and E14): the list replaces the previous one and each category is associated only once.
    // The ProfileService checks before that the list is not empty and that the new categories are active.
    public void ReplaceCategories(IReadOnlyCollection<ServiceCategory> categories)
    {
        var distinct = categories
            .GroupBy(c => c.ServiceCategoryId)
            .Select(g => g.First())
            .ToList();

        var currentIds = _serviceAreas.Select(a => a.ServiceCategoryId).ToHashSet();
        var newIds = distinct.Select(c => c.ServiceCategoryId).ToHashSet();
        _serviceAreas.RemoveAll(a => !newIds.Contains(a.ServiceCategoryId));
        foreach (var category in distinct.Where(c => !currentIds.Contains(c.ServiceCategoryId)))
        {
            _serviceAreas.Add(new ServiceArea(category));
        }
    }

    // RF-F015-01 (E17): a new radius replaces the previous one. The range is validated by the DTO.
    public void SetServiceRadius(short serviceRadiusKm)
    {
        ServiceRadiusKm = serviceRadiusKm;
    }
}
