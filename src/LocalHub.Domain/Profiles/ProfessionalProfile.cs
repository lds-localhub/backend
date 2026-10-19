using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;

namespace LocalHub.Domain.Profiles;

/// <summary>
/// Represents the professional profile of a service provider (F004): presentation, contacts, experience,
/// categories in which the provider works and service radius.
/// </summary>
public class ProfessionalProfile
{
    // Limits of the API documentation (E12), the same as the columns of the data model.
    public const int IntroductionMaxLength = 1000;
    public const int ContactDetailsMaxLength = 500;
    public const int ExperienceMaxLength = 2000;

    public const string IntroductionField = "introduction";
    public const string ContactDetailsField = "contactDetails";
    public const string ExperienceField = "experience";
    public const string CategoriesField = "serviceCategoryIds";

    public int ProfessionalProfileId { get; private set; }
    public int ServiceProviderId { get; private set; }
    // "= null!": always set by Create or loaded by Entity Framework with Include.
    public User ServiceProvider { get; private set; } = null!;
    public string Introduction { get; private set; } = null!;
    public string ContactDetails { get; private set; } = null!;
    public string Experience { get; private set; } = null!;
    // Derived from the reviews (F012); null while the provider has no reviews. Never changed by the provider
    // (RF-F004-02, rule 3).
    public decimal? AverageRating { get; private set; }
    public short ServiceRadiusKm { get; private set; }

    private readonly List<ServiceArea> _serviceAreas = [];
    public IReadOnlyCollection<ServiceArea> ServiceAreas => _serviceAreas;

    // Private: a profile is created with Create. Entity Framework uses this constructor when it reads a profile.
    private ProfessionalProfile() { }

    // RF-F004-01. "One profile per provider" depends on the database, so it is checked by the ProfileService.
    // The location of the provider is saved in User (E12 converts the address before calling Create).
    public static ProfessionalProfile Create(
        User provider,
        string? introduction,
        string? contactDetails,
        string? experience,
        IReadOnlyCollection<ServiceCategory> categories,
        decimal? serviceRadiusKm)
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
            Introduction = ValidateIntroduction(introduction),
            ContactDetails = ValidateContactDetails(contactDetails),
            Experience = ValidateExperience(experience),
            ServiceRadiusKm = ServiceRadius.Validate(serviceRadiusKm),
        };
        profile.ReplaceCategories(categories);

        return profile;
    }

    // RF-F004-02 (E14): the presentation, the contacts and the experience. The rules are the ones of Create.
    public void UpdateDetails(string? introduction, string? contactDetails, string? experience)
    {
        string newIntroduction = ValidateIntroduction(introduction);
        string newContactDetails = ValidateContactDetails(contactDetails);
        string newExperience = ValidateExperience(experience);

        Introduction = newIntroduction;
        ContactDetails = newContactDetails;
        Experience = newExperience;
    }

    // RF-F004-04 (E12 and E14): the list replaces the previous one. The profile keeps at least one category,
    // each category is associated only once and only active categories can be added. A category that was
    // already associated can stay even if it was deactivated in the meantime (API documentation, E14).
    public void ReplaceCategories(IReadOnlyCollection<ServiceCategory> categories)
    {
        var distinct = categories
            .GroupBy(c => c.ServiceCategoryId)
            .Select(g => g.First())
            .ToList();

        if (distinct.Count == 0)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Indique pelo menos uma categoria de serviços.", CategoriesField);
        }

        var currentIds = _serviceAreas.Select(a => a.ServiceCategoryId).ToHashSet();
        var inactive = distinct
            .Where(c => !currentIds.Contains(c.ServiceCategoryId) && c.Status != ActivationStatus.Active)
            .Select(c => c.Name)
            .ToList();
        if (inactive.Count > 0)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"Só pode associar categorias ativas (desativada: {string.Join(", ", inactive)}).", CategoriesField);
        }

        var newIds = distinct.Select(c => c.ServiceCategoryId).ToHashSet();
        _serviceAreas.RemoveAll(a => !newIds.Contains(a.ServiceCategoryId));
        foreach (var category in distinct.Where(c => !currentIds.Contains(c.ServiceCategoryId)))
        {
            _serviceAreas.Add(new ServiceArea(category));
        }
    }

    // RF-F015-01 (E17): a new radius replaces the previous one.
    public void SetServiceRadius(decimal? serviceRadiusKm)
    {
        ServiceRadiusKm = ServiceRadius.Validate(serviceRadiusKm);
    }

    // Public so that the ProfileService can collect the errors of every field in a single 400 response.
    public static string ValidateIntroduction(string? value) =>
        ValidateText(value, IntroductionMaxLength, IntroductionField,
            $"A apresentação é obrigatória e tem no máximo {IntroductionMaxLength} caracteres.");

    public static string ValidateContactDetails(string? value) =>
        ValidateText(value, ContactDetailsMaxLength, ContactDetailsField,
            $"Os contactos são obrigatórios e têm no máximo {ContactDetailsMaxLength} caracteres.");

    public static string ValidateExperience(string? value) =>
        ValidateText(value, ExperienceMaxLength, ExperienceField,
            $"A experiência profissional é obrigatória e tem no máximo {ExperienceMaxLength} caracteres.");

    // Same rule as the CHECK constraints of the table: the text cannot be empty or only spaces.
    private static string ValidateText(string? value, int maxLength, string field, string message)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new DomainException(DomainErrorKind.Validation, message, field);
        }

        return trimmed;
    }
}