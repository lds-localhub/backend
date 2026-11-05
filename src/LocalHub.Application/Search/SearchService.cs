using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles;
using LocalHub.Application.Profiles.Dtos;
using LocalHub.Application.Reviews;
using LocalHub.Application.Search.Dtos;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Geocoding;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Search;

public class SearchService
{
    private const string CategoryField = "serviceCategoryId";

    private readonly LocalHubDbContext _db;
    private readonly LocationResolver _locationResolver;
    private readonly ICurrentUser _currentUser;

    public SearchService(LocalHubDbContext db, LocationResolver locationResolver, ICurrentUser currentUser)
    {
        _db = db;
        _locationResolver = locationResolver;
        _currentUser = currentUser;
    }

    // E18 (RF-F005-01, RF-F005-02, RF-F009-02): the providers of an active category, ordered by the distance to the
    // reference location. The service radius does not limit the results; each result tells whether the reference
    // location is inside it. The search is public and never changes the registered location.
    public async Task<PagedResult<ProviderSearchResult>> SearchProvidersAsync(
        ProviderSearchRequest request, PageRequest page, CancellationToken cancellationToken)
    {
        // The category is required (checked by the controller, rule R17).
        var errors = new Dictionary<string, string[]>();
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        int serviceCategoryId = request.ServiceCategoryId!.Value;
        await EnsureCategoryIsActiveAsync(serviceCategoryId, cancellationToken);

        // Last, because an address is sent to the Nominatim: not found → 400, Nominatim down → 503.
        Coordinates reference = await _locationResolver.ResolveReferenceAsync(
            request.Address, await FindClientLocationAsync(cancellationToken), cancellationToken);

        // Only providers with an active account, a profile with the category and a location (the radius is always
        // defined in the profile) — RF-F005-01, rule 3; RF-F015-05.
        var profiles = _db.ProfessionalProfiles.AsNoTracking()
            .Where(p => p.ServiceProvider.Status == ActivationStatus.Active
                        && p.ServiceProvider.Latitude != null
                        && p.ServiceProvider.Longitude != null
                        && p.ServiceAreas.Any(a => a.ServiceCategoryId == serviceCategoryId));

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            // Part of the name, ignoring upper and lower case (RF-F009-02, rule 2). As in CategoryService, ToLower() is
            // translated to lower() in SQL, so the culture and StringComparison asked by the analyzers do not apply.
            string name = request.Name.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862
            profiles = profiles.Where(p => p.ServiceProvider.Name.ToLower().Contains(name));
#pragma warning restore CA1304, CA1311, CA1862
        }

        int total = await profiles.CountAsync(cancellationToken);
        var providers = await profiles
            .Select(p => new
            {
                p.ProfessionalProfileId,
                p.ServiceProvider.Name,
                Categories = p.ServiceAreas
                    .Select(a => new ProfileCategoryResponse(a.ServiceCategoryId, a.ServiceCategory.Name))
                    .ToList(),
                p.AverageRating,
                p.ServiceRadiusKm,
                Latitude = p.ServiceProvider.Latitude!.Value,
                Longitude = p.ServiceProvider.Longitude!.Value,
            })
            .ToListAsync(cancellationToken);

        // Calculated in memory (DEC-06), so the page is chosen after ordering by the distance.
        var rows = providers
            .Select(p => new
            {
                Provider = p,
                DistanceKm = GeoDistance.HaversineKm(p.Latitude, p.Longitude, reference.Latitude, reference.Longitude),
            })
            .OrderBy(r => r.DistanceKm)
            .ThenBy(r => r.Provider.ProfessionalProfileId) // stable order between pages when two providers are at the same distance
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToList();

        // Only for the providers of the page, in a single query.
        var reviewCounts = await _db.CountReviewsAsync(
            rows.Select(r => r.Provider.ProfessionalProfileId).ToList(), cancellationToken);

        var items = rows
            .Select(r => new ProviderSearchResult(
                r.Provider.ProfessionalProfileId,
                r.Provider.Name,
                ProfileCategoryResponse.InDisplayOrder(r.Provider.Categories),
                r.Provider.AverageRating,
                reviewCounts.GetValueOrDefault(r.Provider.ProfessionalProfileId),
                GeoDistance.RoundKm(r.DistanceKm),
                r.Provider.ServiceRadiusKm,
                // The exact distance, not the rounded one.
                r.DistanceKm <= r.Provider.ServiceRadiusKm))
            .ToList();

        return new PagedResult<ProviderSearchResult>(page.Page, page.PageSize, total, items);
    }

    // E19 (RF-F006-01): the PUBLIC services that are OPEN and whose Client is ACTIVE, ordered by the distance to the
    // registered location of the provider (no other location can be indicated). Without withinServiceRadius the
    // service radius does not limit the results; with it, only the services at a distance equal to or less than the
    // radius are returned. The data of the Client is never returned.
    public async Task<PagedResult<OpenServiceSearchResult>> SearchOpenServicesAsync(
        int? serviceCategoryId, bool withinServiceRadius, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var provider = await FindProviderAsync(cancellationToken);
        if (serviceCategoryId != null)
        {
            await EnsureCategoryIsActiveAsync(serviceCategoryId.Value, cancellationToken);
        }

        // The profile always has a location (E12 and E14 require the address); without it there is no distance.
        if (provider.Location == null)
        {
            var locationErrors = new FieldErrors();
            locationErrors.Add("address", "Indique a morada no seu perfil profissional para pesquisar trabalhos publicados.");
            locationErrors.ThrowIfAny();
        }

        // ThrowIfAny above always throws without a location.
        Coordinates reference = provider.Location!;

        // RF-F006-01: the PUBLIC services that are OPEN and whose Client is ACTIVE.
        var services = _db.Services.AsNoTracking()
            .Where(s => s.ProposalModel == ProposalModel.Public
                        && s.Status == ServiceStatus.Open
                        && s.Client.Status == ActivationStatus.Active);
        if (serviceCategoryId != null)
        {
            services = services.Where(s => s.ServiceCategoryId == serviceCategoryId);
        }

        var candidates = await services
            .Select(s => new
            {
                s.ServiceId,
                s.ServiceCategoryId,
                CategoryName = s.ServiceCategory.Name,
                s.Title,
                s.Description,
                s.Location,
                s.Latitude,
                s.Longitude,
                s.RequestedStartDate,
                s.RequestedEndDate,
                s.CreationDate,
            })
            .ToListAsync(cancellationToken);

        // Calculated in memory (DEC-06), so the radius filter, the total and the page are applied after the distance.
        var withDistance = candidates
            .Select(s => new
            {
                Service = s,
                DistanceKm = GeoDistance.HaversineKm(s.Latitude, s.Longitude, reference.Latitude, reference.Longitude),
            })
            .ToList();
        if (withinServiceRadius)
        {
            // The exact distance, not the rounded one, as in withinServiceRadius.
            withDistance = withDistance.Where(r => r.DistanceKm <= provider.ServiceRadiusKm).ToList();
        }

        int total = withDistance.Count;
        var rows = withDistance
            .OrderBy(r => r.DistanceKm)
            .ThenBy(r => r.Service.ServiceId) // stable order between pages when two services are at the same distance
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToList();

        var items = rows
            .Select(r => new OpenServiceSearchResult(
                r.Service.ServiceId,
                new ServiceCategorySummary(r.Service.ServiceCategoryId, r.Service.CategoryName),
                r.Service.Title,
                r.Service.Description,
                r.Service.Location,
                GeoDistance.RoundKm(r.DistanceKm),
                r.DistanceKm <= provider.ServiceRadiusKm,
                r.Service.RequestedStartDate,
                r.Service.RequestedEndDate,
                r.Service.CreationDate))
            .ToList();

        return new PagedResult<OpenServiceSearchResult>(page.Page, page.PageSize, total, items);
    }

    // E38 (RF-F011-01): the detail of a PUBLIC service, for the provider to decide whether to present a proposal (E25).
    // A service that does not exist, has direct requests, is no longer OPEN or whose Client is INACTIVE is not found
    // (404).
    public async Task<OpenServiceResponse> GetOpenServiceAsync(int serviceId, CancellationToken cancellationToken)
    {
        var provider = await FindProviderAsync(cancellationToken);

        var service = await _db.Services.AsNoTracking()
            .Where(s => s.ServiceId == serviceId
                        && s.ProposalModel == ProposalModel.Public
                        && s.Status == ServiceStatus.Open
                        && s.Client.Status == ActivationStatus.Active)
            .Select(s => new
            {
                s.ServiceId,
                s.ServiceCategoryId,
                CategoryName = s.ServiceCategory.Name,
                s.Title,
                s.Description,
                s.Location,
                s.Latitude,
                s.Longitude,
                s.RequestedStartDate,
                s.RequestedEndDate,
                s.CreationDate,
                // Only the own proposal: never those of other providers (RNF-06).
                MyProposalId = s.Proposals
                    .Where(p => p.ProfessionalProfileId == provider.ProfessionalProfileId)
                    .Select(p => (int?)p.ProposalId)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (service == null)
        {
            throw new NotFoundException("Trabalho publicado não encontrado.");
        }

        bool withinServiceRadius = provider.Location != null
                                   && GeoDistance.HaversineKm(
                                       provider.Location.Latitude,
                                       provider.Location.Longitude,
                                       service.Latitude,
                                       service.Longitude)
                                   <= provider.ServiceRadiusKm;

        return new OpenServiceResponse(
            service.ServiceId,
            new ServiceCategorySummary(service.ServiceCategoryId, service.CategoryName),
            service.Title,
            service.Description,
            service.Location,
            LocationQueries.DistanceKm(provider.Location, service.Latitude, service.Longitude),
            withinServiceRadius,
            service.RequestedStartDate,
            service.RequestedEndDate,
            service.CreationDate,
            service.MyProposalId);
    }

    // The profile of the provider that searches, with its service radius and location. The RequireProfessionalProfileFilter
    // already refuses a provider without a profile; this is the same rule in the application.
    private async Task<ProviderReference> FindProviderAsync(CancellationToken cancellationToken)
    {
        var profile = await _db.ProfessionalProfiles.AsNoTracking()
            .Where(p => p.ServiceProviderId == _currentUser.UserId)
            .Select(p => new { p.ProfessionalProfileId, p.ServiceRadiusKm })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile == null)
        {
            throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);
        }

        return new ProviderReference(
            profile.ProfessionalProfileId,
            profile.ServiceRadiusKm,
            await _db.FindLocationAsync(_currentUser.UserId, cancellationToken));
    }

    private sealed record ProviderReference(int ProfessionalProfileId, short ServiceRadiusKm, Coordinates? Location);

    // RF-F005-01, rule 2: only active categories can be searched.
    private async Task EnsureCategoryIsActiveAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        bool isActive = await _db.ServiceCategories.AnyAsync(
            c => c.ServiceCategoryId == serviceCategoryId && c.Status == ActivationStatus.Active,
            cancellationToken);

        if (!isActive)
        {
            throw new ValidationException(CategoryField, "A categoria indicada não existe ou está desativada.");
        }
    }

    // RF-F005-02, rule 1: the registered location of a client is used when the search has no other location.
    // Visitors and the other roles always indicate it.
    private async Task<Coordinates?> FindClientLocationAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.Role != UserRole.Client)
        {
            return null;
        }

        var location = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId && u.Latitude != null && u.Longitude != null)
            .Select(u => new { Latitude = u.Latitude!.Value, Longitude = u.Longitude!.Value })
            .SingleOrDefaultAsync(cancellationToken);

        return location == null ? null : Coordinates.Rounded(location.Latitude, location.Longitude);
    }
}