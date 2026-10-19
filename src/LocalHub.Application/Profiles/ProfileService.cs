using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Locations.Dtos;
using LocalHub.Application.Profiles.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Profiles;

public class ProfileService(LocalHubDbContext db, LocationResolver locationResolver, ICurrentUser currentUser)
{
    private const string AddressField = "address";
    private const string ServiceRadiusField = "serviceRadiusKm";
    private const string ProfileExistsMessage = "Já tem um perfil profissional.";
    public const string MissingProfileMessage = "Crie primeiro o seu perfil profissional.";

    // Used by the filter of the endpoints that need a profile (API documentation, section 7).
    public Task<int?> GetMyProfileIdAsync(CancellationToken cancellationToken) =>
        db.FindProfileIdAsync(currentUser.UserId, cancellationToken);

    // E12 (RF-F004-01, RF-F015-01): the profile and the first location of the provider are saved together.
    public async Task<ProfileResponse> CreateAsync(CreateProfileRequest request, CancellationToken cancellationToken)
    {
        var provider = await db.Users.SingleOrDefaultAsync(u => u.UserId == currentUser.UserId, cancellationToken)
                       ?? throw new NotFoundException("Utilizador não encontrado.");

        if (await db.ProfessionalProfiles.AnyAsync(p => p.ServiceProviderId == provider.UserId, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, ProfileExistsMessage);
        }

        // First the rules that do not need the database or the Nominatim, all reported in the same response.
        var errors = new Dictionary<string, string[]>();
        CollectDetailErrors(errors, request.Introduction, request.ContactDetails, request.Experience);
        CollectCategoryListError(errors, request.ServiceCategoryIds);
        Collect(errors, () => ServiceRadius.Validate(request.ServiceRadiusKm), ServiceRadiusField);
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            errors[AddressField] = ["A morada é obrigatória."];
        }

        ThrowIfAny(errors);

        var categories = await LoadCategoriesAsync(request.ServiceCategoryIds!, cancellationToken);
        var profile = ProfessionalProfile.Create(
            provider, request.Introduction, request.ContactDetails, request.Experience, categories,
            request.ServiceRadiusKm);

        // Last, because it calls the Nominatim: an address that is not found (400) or a Nominatim that is down (503)
        // leaves nothing saved.
        var location = await locationResolver.ResolveAsync(
            new SetLocationRequest(request.Address, null, null), cancellationToken);
        provider.SetLocation(location.Address, location.Coordinates);

        db.ProfessionalProfiles.Add(profile);
        await SaveNewProfileAsync(cancellationToken);

        return ProfileResponse.From(profile);
    }

    // E13 (RF-F004-03): a client sees the profile of an active provider; a provider only sees its own profile.
    // A provider without a profile is stopped before, by the filter (403 perfil-em-falta).
    public async Task<ProfileResponse> GetAsync(int professionalProfileId, CancellationToken cancellationToken)
    {
        var profile = await WithDetails(db.ProfessionalProfiles.AsNoTracking())
            .SingleOrDefaultAsync(p => p.ProfessionalProfileId == professionalProfileId, cancellationToken);

        if (currentUser.Role == UserRole.Provider)
        {
            if (profile is null)
            {
                throw new NotFoundException("Perfil não encontrado.");
            }

            if (profile.ServiceProviderId != currentUser.UserId)
            {
                throw new ForbiddenException("Só pode consultar o seu próprio perfil.");
            }

            return ProfileResponse.From(profile);
        }

        // The profile of a provider with a deactivated account is not shown to the client (RF-F004-03, rule 2).
        if (profile is null || profile.ServiceProvider.Status != ActivationStatus.Active)
        {
            throw new NotFoundException("Perfil não encontrado.");
        }

        return ProfileResponse.From(profile);
    }

    // E14 (RF-F004-02, RF-F004-04): the list of categories replaces the previous one.
    public async Task<ProfileResponse> UpdateMineAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        CollectDetailErrors(errors, request.Introduction, request.ContactDetails, request.Experience);
        CollectCategoryListError(errors, request.ServiceCategoryIds);
        ThrowIfAny(errors);

        var profile = await FindMineAsync(cancellationToken);
        var categories = await LoadCategoriesAsync(request.ServiceCategoryIds!, cancellationToken);

        profile.UpdateDetails(request.Introduction, request.ContactDetails, request.Experience);
        profile.ReplaceCategories(categories);
        await db.SaveChangesAsync(cancellationToken);

        return ProfileResponse.From(profile);
    }

    // E17 (RF-F015-01): the new radius replaces the previous one.
    public async Task<ServiceRadiusResponse> SetMyServiceRadiusAsync(
        ServiceRadiusRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.ProfessionalProfiles
            .SingleOrDefaultAsync(p => p.ServiceProviderId == currentUser.UserId, cancellationToken)
            ?? throw new ForbiddenException(MissingProfileMessage, ErrorTypes.MissingProfile);

        profile.SetServiceRadius(request.ServiceRadiusKm);
        await db.SaveChangesAsync(cancellationToken);

        return new ServiceRadiusResponse(profile.ServiceRadiusKm);
    }

    private static IQueryable<ProfessionalProfile> WithDetails(IQueryable<ProfessionalProfile> query) =>
        query
            .Include(p => p.ServiceProvider)
            .Include(p => p.ServiceAreas)
            .ThenInclude(a => a.ServiceCategory);

    private async Task<ProfessionalProfile> FindMineAsync(CancellationToken cancellationToken)
    {
        return await WithDetails(db.ProfessionalProfiles)
                   .SingleOrDefaultAsync(p => p.ServiceProviderId == currentUser.UserId, cancellationToken)
               ?? throw new ForbiddenException(MissingProfileMessage, ErrorTypes.MissingProfile);
    }

    // Every requested category must exist; whether it can be associated (active, or already associated) is
    // decided by ProfessionalProfile (RF-F004-04).
    private async Task<IReadOnlyCollection<ServiceCategory>> LoadCategoriesAsync(
        IReadOnlyList<int> serviceCategoryIds, CancellationToken cancellationToken)
    {
        var ids = serviceCategoryIds.Distinct().ToList();
        var categories = await db.ServiceCategories
            .Where(c => ids.Contains(c.ServiceCategoryId))
            .ToListAsync(cancellationToken);

        var missing = ids.Except(categories.Select(c => c.ServiceCategoryId)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException(DomainErrorKind.Validation,
                $"Categoria inexistente: {string.Join(", ", missing)}.", ProfessionalProfile.CategoriesField);
        }

        return categories;
    }

    private static void CollectDetailErrors(
        Dictionary<string, string[]> errors, string? introduction, string? contactDetails, string? experience)
    {
        Collect(errors, () => ProfessionalProfile.ValidateIntroduction(introduction), ProfessionalProfile.IntroductionField);
        Collect(errors, () => ProfessionalProfile.ValidateContactDetails(contactDetails),
            ProfessionalProfile.ContactDetailsField);
        Collect(errors, () => ProfessionalProfile.ValidateExperience(experience), ProfessionalProfile.ExperienceField);
    }

    private static void CollectCategoryListError(Dictionary<string, string[]> errors, IReadOnlyList<int>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            errors[ProfessionalProfile.CategoriesField] = ["Indique pelo menos uma categoria de serviços."];
        }
    }

    private static void Collect<T>(Dictionary<string, string[]> errors, Func<T> validate, string field)
    {
        try
        {
            validate();
        }
        catch (DomainException ex) when (ex.Kind == DomainErrorKind.Validation)
        {
            errors[ex.Field ?? field] = [ex.Message];
        }
    }

    private static void ThrowIfAny(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private async Task SaveNewProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UQ_ProfessionalProfile_serviceProviderId"
        })
        {
            // Two E12 requests of the same provider at the same time: the unique index rejects the second one.
            throw new DomainException(DomainErrorKind.Conflict, ProfileExistsMessage);
        }
    }
}