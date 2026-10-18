namespace LocalHub.Infrastructure.Categories;

// Tells whether a category is still used by providers or services, so that it cannot be deactivated (RF-F003-03).
public interface ICategoryUsage
{
    Task<CategoryUsageCount> CountAsync(int serviceCategoryId, CancellationToken cancellationToken);
}

// ActiveProviders: providers with an ACTIVE account and the category in their professional profile.
// ServicesInProgress: services of the category in the state OPEN or ASSIGNED (RF-F003-03, rule 1).
public record CategoryUsageCount(int ActiveProviders, int ServicesInProgress)
{
    public bool IsInUse => ActiveProviders > 0 || ServicesInProgress > 0;
}