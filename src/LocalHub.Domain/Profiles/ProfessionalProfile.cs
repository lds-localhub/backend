using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Services;

namespace LocalHub.Domain.Profiles;

/// <summary>
/// The professional profile of a service provider: presentation, contacts, experience, categories and service radius.
/// </summary>
public class ProfessionalProfile
{
    public const int IntroductionMaxLength = 1000;
    public const int ContactDetailsMaxLength = 500;
    public const int ExperienceMaxLength = 2000;
    public const int ServiceRadiusMinKm = 1;
    public const int ServiceRadiusMaxKm = 100;

    public int ProfessionalProfileId { get; private set; }
    public int ServiceProviderId { get; private set; }
    public string Introduction { get; private set; } = null!;
    public string ContactDetails { get; private set; } = null!;
    public string Experience { get; private set; } = null!;
    public decimal? AverageRating { get; private set; }
    public short ServiceRadiusKm { get; private set; }

    // Navigations: set by Create or loaded by Entity Framework with Include.
    public User ServiceProvider { get; private set; } = null!;
    private readonly List<ServiceArea> _serviceAreas = new();
    public IReadOnlyCollection<ServiceArea> ServiceAreas => _serviceAreas;
    private readonly List<Proposal> _proposals = new();
    public IReadOnlyCollection<Proposal> Proposals => _proposals;
    private readonly List<DirectRequest> _directRequests = new();
    public IReadOnlyCollection<DirectRequest> DirectRequests => _directRequests;

    // Used by Entity Framework; a profile is created with Create.
    private ProfessionalProfile() { }

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
        profile.SetCategories(categories);

        return profile;
    }

    public void Update(string introduction, string contactDetails, string experience, short serviceRadiusKm)
    {
        Introduction = introduction.Trim();
        ContactDetails = contactDetails.Trim();
        Experience = experience.Trim();
        ServiceRadiusKm = serviceRadiusKm;
    }

    public void SetCategories(IReadOnlyCollection<ServiceCategory> categories)
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

    // One decimal place (0.05 rounds up); null without reviews.
    public void RecalculateAverageRating(IReadOnlyCollection<short> ratings)
    {
        AverageRating = CalculateAverageRating(ratings);
    }

    public static decimal? CalculateAverageRating(IReadOnlyCollection<short> ratings)
    {
        return ratings.Count == 0
            ? null
            : Math.Round((decimal)ratings.Sum(r => (int)r) / ratings.Count, 1, MidpointRounding.AwayFromZero);
    }
}