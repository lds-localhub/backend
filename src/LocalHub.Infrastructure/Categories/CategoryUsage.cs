using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure.Categories;

// ActiveProviders counts the profiles of the category whose provider has an ACTIVE account (F004).
// ServicesInProgress counts the services of the category in the state OPEN or ASSIGNED (RF-F003-03, rule 1).
public class CategoryUsage : ICategoryUsage
{
    private readonly LocalHubDbContext _db;

    public CategoryUsage(LocalHubDbContext db)
    {
        _db = db;
    }

    public async Task<CategoryUsageCount> CountAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        int activeProviders = await _db.ProfessionalProfiles
            .Where(p => p.ServiceProvider.Status == ActivationStatus.Active
                        && p.ServiceAreas.Any(a => a.ServiceCategoryId == serviceCategoryId))
            .CountAsync(cancellationToken);

        int servicesInProgress = await _db.Services
            .Where(s => s.ServiceCategoryId == serviceCategoryId
                        && (s.Status == ServiceStatus.Open || s.Status == ServiceStatus.Assigned))
            .CountAsync(cancellationToken);

        return new CategoryUsageCount(activeProviders, servicesInProgress);
    }
}