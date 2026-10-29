using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Locations.Dtos;
using LocalHub.Application.Profiles.Dtos;
using LocalHub.Application.Reviews;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Profiles;

public class ProfileService
{
    private const string CategoriesField = "serviceCategoryIds";
    private const string ProfileExistsMessage = "Já tem um perfil profissional.";
    public const string MissingProfileMessage = "Crie primeiro o seu perfil profissional.";

    private readonly LocalHubDbContext _db;
    private readonly LocationResolver _locationResolver;
    private readonly ICurrentUser _currentUser;

    public ProfileService(LocalHubDbContext db, LocationResolver locationResolver, ICurrentUser currentUser)
    {
        _db = db;
        _locationResolver = locationResolver;
        _currentUser = currentUser;
    }

    // Used by the filter of the endpoints that need a profile (API documentation, section 7).
    public Task<int?> GetMyProfileIdAsync(CancellationToken cancellationToken) =>
        _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);

    // E12 (RF-F004-01, RF-F015-01): the profile and the first location of the provider are saved together.
    // The fields were validated by the DTO; here are the rules that need the database or the Nominatim (rule R17).
    public async Task<ProfileResponse> CreateAsync(CreateProfileRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.Users.SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (provider is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        if (await _db.ProfessionalProfiles.AnyAsync(p => p.ServiceProviderId == provider.UserId, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, ProfileExistsMessage);
        }

        var categories = await LoadCategoriesAsync(request.ServiceCategoryIds!, currentIds: new HashSet<int>(), cancellationToken);
        var profile = ProfessionalProfile.Create(
            provider, request.Introduction!, request.ContactDetails!, request.Experience!, categories,
            request.ServiceRadiusKm!.Value);

        // Last, because it calls the Nominatim: an address that is not found (400) or a Nominatim that is down (503)
        // leaves nothing saved.
        var location = await _locationResolver.ResolveAsync(
            new SetLocationRequest(request.Address, null, null), cancellationToken);
        provider.SetLocation(location.Address, location.Coordinates.Latitude, location.Coordinates.Longitude);

        _db.ProfessionalProfiles.Add(profile);
        await SaveNewProfileAsync(cancellationToken);

        // A new profile has no reviews.
        return ProfileResponse.From(profile, reviewCount: 0);
    }

    // E13 (RF-F004-03): a client sees the profile of an active provider; a provider only sees its own profile.
    // A provider without a profile is stopped before, by the filter (403 perfil-em-falta).
    public async Task<ProfileResponse> GetAsync(int professionalProfileId, CancellationToken cancellationToken)
    {
        var profile = await WithDetails(_db.ProfessionalProfiles.AsNoTracking())
            .SingleOrDefaultAsync(p => p.ProfessionalProfileId == professionalProfileId, cancellationToken);

        if (_currentUser.Role == UserRole.Provider)
        {
            if (profile is null)
            {
                throw new NotFoundException("Perfil não encontrado.");
            }

            if (profile.ServiceProviderId != _currentUser.UserId)
            {
                throw new ForbiddenException("Só pode consultar o seu próprio perfil.");
            }

            return await ToResponseAsync(profile, cancellationToken);
        }

        // The profile of a provider with a deactivated account is not shown to the client (RF-F004-03, rule 2).
        if (profile is null || profile.ServiceProvider.Status != ActivationStatus.Active)
        {
            throw new NotFoundException("Perfil não encontrado.");
        }

        return await ToResponseAsync(profile, cancellationToken);
    }

    // E14 (RF-F004-02, RF-F004-04): the list of categories replaces the previous one.
    public async Task<ProfileResponse> UpdateMineAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await FindMineAsync(cancellationToken);
        var currentIds = profile.ServiceAreas.Select(a => a.ServiceCategoryId).ToHashSet();
        var categories = await LoadCategoriesAsync(request.ServiceCategoryIds!, currentIds, cancellationToken);

        profile.UpdateDetails(request.Introduction!, request.ContactDetails!, request.Experience!);
        profile.ReplaceCategories(categories);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(profile, cancellationToken);
    }

    // The profile with the number of reviews received (F012), shown in the summary of the reviews (RF-F004-03).
    private async Task<ProfileResponse> ToResponseAsync(ProfessionalProfile profile, CancellationToken cancellationToken)
    {
        int reviewCount = await _db.ReviewsOf(profile.ProfessionalProfileId).CountAsync(cancellationToken);
        return ProfileResponse.From(profile, reviewCount);
    }

    // E17 (RF-F015-01): the new radius replaces the previous one. The range was validated by the DTO.
    public async Task<ServiceRadiusResponse> SetMyServiceRadiusAsync(
        ServiceRadiusRequest request, CancellationToken cancellationToken)
    {
        var profile = await _db.ProfessionalProfiles
            .SingleOrDefaultAsync(p => p.ServiceProviderId == _currentUser.UserId, cancellationToken);
        if (profile is null)
        {
            throw new ForbiddenException(MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        profile.SetServiceRadius(request.ServiceRadiusKm!.Value);
        await _db.SaveChangesAsync(cancellationToken);

        return new ServiceRadiusResponse(profile.ServiceRadiusKm);
    }

    private static IQueryable<ProfessionalProfile> WithDetails(IQueryable<ProfessionalProfile> query) =>
        query
            .Include(p => p.ServiceProvider)
            .Include(p => p.ServiceAreas)
            .ThenInclude(a => a.ServiceCategory);

    private async Task<ProfessionalProfile> FindMineAsync(CancellationToken cancellationToken)
    {
        var profile = await WithDetails(_db.ProfessionalProfiles)
            .SingleOrDefaultAsync(p => p.ServiceProviderId == _currentUser.UserId, cancellationToken);
        if (profile is null)
        {
            throw new ForbiddenException(MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        return profile;
    }

    // RF-F004-04: every requested category must exist and only active categories can be added. A category that was
    // already associated (currentIds) can stay even if it was deactivated in the meantime (API documentation, E14).
    private async Task<IReadOnlyCollection<ServiceCategory>> LoadCategoriesAsync(
        IReadOnlyList<int> serviceCategoryIds, HashSet<int> currentIds, CancellationToken cancellationToken)
    {
        var ids = serviceCategoryIds.Distinct().ToList();
        var categories = await _db.ServiceCategories
            .Where(c => ids.Contains(c.ServiceCategoryId))
            .ToListAsync(cancellationToken);

        var missing = ids.Except(categories.Select(c => c.ServiceCategoryId)).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationException(CategoriesField, $"Categoria inexistente: {string.Join(", ", missing)}.");
        }

        var inactive = categories
            .Where(c => !currentIds.Contains(c.ServiceCategoryId) && c.Status != ActivationStatus.Active)
            .Select(c => c.Name)
            .ToList();
        if (inactive.Count > 0)
        {
            throw new ValidationException(CategoriesField,
                $"Só pode associar categorias ativas (desativada: {string.Join(", ", inactive)}).");
        }

        return categories;
    }

    private async Task SaveNewProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two E12 requests of the same provider at the same time: the UNIQUE of serviceProviderId (created by the
            // 1:1 relationship with User) rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainException(DomainErrorKind.Conflict, ProfileExistsMessage);
            }

            // Any other error of the database is not expected: the request gets 500.
            throw;
        }
    }
}