using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure.Categories;

// ActiveProviders counts the profiles of the category whose provider has an ACTIVE account (F004).
// ServicesInProgress counts the services of the category in the state OPEN or ASSIGNED (RF-F003-03, rule 1).
public class CategoryUsage(LocalHubDbContext db) : ICategoryUsage
{
    public async Task<CategoryUsageCount> CountAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        int activeProviders = await db.ProfessionalProfiles
            .Where(p => p.ServiceProvider.Status == ActivationStatus.Active
                        && p.ServiceAreas.Any(a => a.ServiceCategoryId == serviceCategoryId))
            .CountAsync(cancellationToken);

        int servicesInProgress = await db.Services
            .Where(s => s.ServiceCategoryId == serviceCategoryId
                        && (s.Status == ServiceStatus.Open || s.Status == ServiceStatus.Assigned))
            .CountAsync(cancellationToken);

        return new CategoryUsageCount(activeProviders, servicesInProgress);
    }
}