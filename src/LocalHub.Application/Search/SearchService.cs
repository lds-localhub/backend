using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles;
using LocalHub.Application.Profiles.Dtos;
using LocalHub.Application.Search.Dtos;
using LocalHub.Application.Services.Dtos;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Services;
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

    // E19 (RF-F006-01): the published jobs that are OPEN and whose Client is ACTIVE, ordered by the distance to the
    // registered location of the provider (no other location can be indicated). Without withinServiceRadius the
    // service radius does not limit the results; with it, only the jobs at a distance equal to or less than the radius
    // are returned. The data of the Client is never returned.
    public async Task<PagedResult<PublishedJobSearchResult>> SearchPublishedJobsAsync(
        int? serviceCategoryId, bool withinServiceRadius, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var provider = await FindProviderAsync(cancellationToken);
        if (serviceCategoryId is not null)
        {
            await EnsureCategoryIsActiveAsync(serviceCategoryId.Value, cancellationToken);
        }

        // The profile always has a location (E12 and E14 require the address); without it there is no distance.
        if (provider.Location is not { } reference)
        {
            throw new DomainException(DomainErrorKind.Validation,
                "Indique a morada no seu perfil profissional para pesquisar trabalhos publicados.", "address");
        }

        var jobs = db.Services.AsNoTracking().Where(Service.IsOpenPublishedJob);
        if (serviceCategoryId is not null)
        {
            jobs = jobs.Where(s => s.ServiceCategoryId == serviceCategoryId);
        }

        // Calculated by PostgreSQL (DEC-06), so the radius filter and the page are applied after the distance.
        var withDistance = jobs.Select(s => new
        {
            Service = s,
            DistanceKm = GeoDistance.HaversineKm(s.Latitude, s.Longitude, reference.Latitude, reference.Longitude),
        });
        if (withinServiceRadius)
        {
            // The exact distance, not the rounded one, as in withinServiceRadius.
            withDistance = withDistance.Where(r => r.DistanceKm <= provider.ServiceRadiusKm);
        }

        int total = await withDistance.CountAsync(cancellationToken);
        var rows = await withDistance
            .OrderBy(r => r.DistanceKm)
            .ThenBy(r => r.Service.ServiceId) // stable order between pages when two jobs are at the same distance
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(r => new
            {
                r.Service.ServiceId,
                r.Service.ServiceCategoryId,
                CategoryName = r.Service.ServiceCategory.Name,
                r.Service.Description,
                r.Service.Location,
                r.Service.RequestedStartDate,
                r.Service.RequestedEndDate,
                r.Service.CreationDate,
                r.DistanceKm,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new PublishedJobSearchResult(
                r.ServiceId,
                new ServiceCategorySummary(r.ServiceCategoryId, r.CategoryName),
                r.Description,
                r.Location,
                GeoDistance.RoundKm(r.DistanceKm),
                r.DistanceKm <= provider.ServiceRadiusKm,
                r.RequestedStartDate,
                r.RequestedEndDate,
                r.CreationDate))
            .ToList();

        return new PagedResult<PublishedJobSearchResult>(page.Page, page.PageSize, total, items);
    }

    // E38 (RF-F011-01): the detail of a published job, for the provider to decide whether to present a proposal (E25).
    // A job that does not exist, has direct requests, is no longer OPEN or whose Client is INACTIVE is not found (404).
    public async Task<PublishedJobResponse> GetPublishedJobAsync(int serviceId, CancellationToken cancellationToken)
    {
        var provider = await FindProviderAsync(cancellationToken);

        var job = await db.Services.AsNoTracking()
            .Where(s => s.ServiceId == serviceId)
            .Where(Service.IsOpenPublishedJob)
            .Select(s => new
            {
                s.ServiceId,
                s.ServiceCategoryId,
                CategoryName = s.ServiceCategory.Name,
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
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trabalho publicado não encontrado.");

        bool withinServiceRadius = provider.Location is { } location
                                   && GeoDistance.HaversineKm(
                                       location.Latitude, location.Longitude, job.Latitude, job.Longitude)
                                   <= provider.ServiceRadiusKm;

        return new PublishedJobResponse(
            job.ServiceId,
            new ServiceCategorySummary(job.ServiceCategoryId, job.CategoryName),
            job.Description,
            job.Location,
            LocationQueries.DistanceKm(provider.Location, job.Latitude, job.Longitude),
            withinServiceRadius,
            job.RequestedStartDate,
            job.RequestedEndDate,
            job.CreationDate,
            job.MyProposalId);
    }

    // The profile of the provider that searches, with its service radius and location. The RequiresProfessionalProfile
    // filter already refuses a provider without a profile; this is the same rule in the application.
    private async Task<ProviderReference> FindProviderAsync(CancellationToken cancellationToken)
    {
        var profile = await db.ProfessionalProfiles.AsNoTracking()
            .Where(p => p.ServiceProviderId == currentUser.UserId)
            .Select(p => new { p.ProfessionalProfileId, p.ServiceRadiusKm })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException(ProfileService.MissingProfileMessage, ErrorTypes.MissingProfile);

        return new ProviderReference(
            profile.ProfessionalProfileId,
            profile.ServiceRadiusKm,
            await db.FindLocationAsync(currentUser.UserId, cancellationToken));
    }

    private sealed record ProviderReference(int ProfessionalProfileId, short ServiceRadiusKm, GeoPoint? Location);

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