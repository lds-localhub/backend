using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
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

    public Task<int?> GetMyProfileIdAsync(CancellationToken cancellationToken) =>
        _db.FindProfileIdAsync(_currentUser.UserId, cancellationToken);

    public async Task<ProfileResponse> CreateAsync(CreateProfileRequest request, CancellationToken cancellationToken)
    {
        var provider = await _db.Users.SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (provider == null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        if (await _db.ProfessionalProfiles.AnyAsync(p => p.ServiceProviderId == provider.UserId, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, ProfileExistsMessage);
        }

        var categories = await LoadCategoriesAsync(request.ServiceCategoryIds!, currentIds: new HashSet<int>(), cancellationToken);

        // Geocode before changing anything, so nothing is saved if it fails.
        var location = await _locationResolver.ResolveAddressAsync(request.Address!, "address", cancellationToken);

        var profile = ProfessionalProfile.Create(
            provider, request.Introduction!, request.ContactDetails!, request.Experience!, categories,
            request.ServiceRadiusKm!.Value);
        provider.SetLocation(location.Address, location.Coordinates.Latitude, location.Coordinates.Longitude);

        _db.ProfessionalProfiles.Add(profile);
        await SaveNewProfileAsync(cancellationToken);

        return ProfileResponse.From(profile, reviewCount: 0);
    }

    public async Task<ProfileResponse> GetAsync(int professionalProfileId, CancellationToken cancellationToken)
    {
        var profile = await WithDetails(_db.ProfessionalProfiles.AsNoTracking())
            .SingleOrDefaultAsync(p => p.ProfessionalProfileId == professionalProfileId, cancellationToken);

        if (_currentUser.Role == UserRole.Provider)
        {
            if (profile == null)
            {
                throw new NotFoundException("Perfil não encontrado.");
            }

            if (profile.ServiceProviderId != _currentUser.UserId)
            {
                throw new ForbiddenException("Só pode consultar o seu próprio perfil.");
            }

            return await ToResponseAsync(profile, cancellationToken);
        }

        // Deactivated providers are hidden from clients.
        if (profile == null || profile.ServiceProvider.Status != ActivationStatus.Active)
        {
            throw new NotFoundException("Perfil não encontrado.");
        }

        return await ToResponseAsync(profile, cancellationToken);
    }

    public async Task<ProfileResponse> UpdateMineAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await FindMineAsync(cancellationToken);
        var currentIds = profile.ServiceAreas.Select(a => a.ServiceCategoryId).ToHashSet();
        var categories = await LoadCategoriesAsync(request.ServiceCategoryIds!, currentIds, cancellationToken);

        // Geocode before changing anything, so nothing is saved if it fails.
        var location = await _locationResolver.ResolveAddressAsync(request.Address!, "address", cancellationToken);

        profile.Update(request.Introduction!, request.ContactDetails!, request.Experience!,
            request.ServiceRadiusKm!.Value);
        profile.SetCategories(categories);
        profile.ServiceProvider.SetLocation(
            location.Address, location.Coordinates.Latitude, location.Coordinates.Longitude);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(profile, cancellationToken);
    }

    private async Task<ProfileResponse> ToResponseAsync(ProfessionalProfile profile, CancellationToken cancellationToken)
    {
        int reviewCount = await _db.ReviewsOf(profile.ProfessionalProfileId).CountAsync(cancellationToken);
        return ProfileResponse.From(profile, reviewCount);
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
        if (profile == null)
        {
            throw new ForbiddenException(MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        return profile;
    }

    // A category that was already associated can stay even if it was deactivated meanwhile.
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
            // Two requests at the same time: the unique serviceProviderId rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainException(DomainErrorKind.Conflict, ProfileExistsMessage);
            }

            throw;
        }
    }
}