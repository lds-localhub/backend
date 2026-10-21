using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles.Dtos;
using LocalHub.Application.Search.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Search;

public class SearchService(LocalHubDbContext db, LocationResolver locationResolver, ICurrentUser currentUser)
{
    private const string CategoryField = "serviceCategoryId";

    // E18 (RF-F005-01, RF-F005-02, RF-F009-02): the providers of an active category, ordered by the distance to the
    // reference location. The service radius does not limit the results; each result tells whether the reference
    // location is inside it. The search is public and never changes the registered location.
    public async Task<PagedResult<ProviderSearchResult>> SearchProvidersAsync(
        ProviderSearchRequest request, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ServiceCategoryId is null)
        {
            errors[CategoryField] = ["Indique a categoria de serviços."];
        }

        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        int serviceCategoryId = request.ServiceCategoryId!.Value;
        await EnsureCategoryIsActiveAsync(serviceCategoryId, cancellationToken);

        // Last, because an address is sent to the Nominatim: not found → 400, Nominatim down → 503.
        GeoPoint reference = await locationResolver.ResolveReferenceAsync(
            request.Address, request.Latitude, request.Longitude, await FindClientLocationAsync(cancellationToken),
            cancellationToken);

        // Only providers with an active account, a profile with the category and a location (the radius is always
        // defined in the profile) — RF-F005-01, rule 3; RF-F015-05.
        var profiles = db.ProfessionalProfiles.AsNoTracking()
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
        var rows = await profiles
            .Select(p => new
            {
                p.ProfessionalProfileId,
                p.ServiceProvider.Name,
                Categories = p.ServiceAreas
                    .Select(a => new ProfileCategoryResponse(a.ServiceCategoryId, a.ServiceCategory.Name))
                    .ToList(),
                p.AverageRating,
                p.ServiceRadiusKm,
                // Calculated by PostgreSQL (DEC-06), so the page is chosen after ordering by the distance.
                DistanceKm = GeoDistance.HaversineKm(
                    p.ServiceProvider.Latitude!.Value,
                    p.ServiceProvider.Longitude!.Value,
                    reference.Latitude,
                    reference.Longitude),
            })
            .OrderBy(r => r.DistanceKm)
            .ThenBy(r => r.ProfessionalProfileId) // stable order between pages when two providers are at the same distance
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ProviderSearchResult(
                r.ProfessionalProfileId,
                r.Name,
                ProfileCategoryResponse.InDisplayOrder(r.Categories),
                r.AverageRating,
                GeoDistance.RoundKm(r.DistanceKm),
                r.ServiceRadiusKm,
                // The exact distance, not the rounded one.
                r.DistanceKm <= r.ServiceRadiusKm))
            .ToList();

        return new PagedResult<ProviderSearchResult>(page.Page, page.PageSize, total, items);
    }

    // RF-F005-01, rule 2: only active categories can be searched.
    private async Task EnsureCategoryIsActiveAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        bool isActive = await db.ServiceCategories.AnyAsync(
            c => c.ServiceCategoryId == serviceCategoryId && c.Status == ActivationStatus.Active,
            cancellationToken);

        if (!isActive)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "A categoria indicada não existe ou está desativada.", CategoryField);
        }
    }

    // RF-F005-02, rule 1: the registered location of a client is used when the search has no other location.
    // Visitors and the other roles always indicate it.
    private async Task<GeoPoint?> FindClientLocationAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.Role != UserRole.Client)
        {
            return null;
        }

        var location = await db.Users.AsNoTracking()
            .Where(u => u.UserId == currentUser.UserId && u.Latitude != null && u.Longitude != null)
            .Select(u => new { Latitude = u.Latitude!.Value, Longitude = u.Longitude!.Value })
            .SingleOrDefaultAsync(cancellationToken);

        return location is null ? null : GeoPoint.Create(location.Latitude, location.Longitude);
    }
}