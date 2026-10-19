using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure.Categories;

// ActiveProviders counts the profiles of the category whose provider has an ACTIVE account (F004).
// PENDING: the services (F007 to F011) are not mapped by Entity Framework yet, so ServicesInProgress is always 0.
// When the entity Service is added, it must count the services of the category in the state OPEN or ASSIGNED.
public class CategoryUsage(LocalHubDbContext db) : ICategoryUsage
{
    public async Task<CategoryUsageCount> CountAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        int activeProviders = await db.ProfessionalProfiles
            .Where(p => p.ServiceProvider.Status == ActivationStatus.Active
                        && p.ServiceAreas.Any(a => a.ServiceCategoryId == serviceCategoryId))
            .CountAsync(cancellationToken);

        return new CategoryUsageCount(activeProviders, ServicesInProgress: 0);
    }
}