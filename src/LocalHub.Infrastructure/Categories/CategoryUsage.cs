namespace LocalHub.Infrastructure.Categories;

// PENDING: the professional profiles (ServiceArea, F004) and the services (F007 to F011) are not mapped by
// Entity Framework yet, so there is nothing to count. When those entities are added, these two counts must be
// replaced by queries to LocalHubDbContext:
// - ActiveProviders: ServiceArea of the category whose profile belongs to a provider with an ACTIVE account;
// - ServicesInProgress: Service of the category in the state OPEN or ASSIGNED.
// The CategoryService and its tests already use the result, so nothing else has to change.
public class CategoryUsage : ICategoryUsage
{
    public Task<CategoryUsageCount> CountAsync(int serviceCategoryId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CategoryUsageCount(ActiveProviders: 0, ServicesInProgress: 0));
    }
}