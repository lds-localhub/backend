namespace LocalHub.Application.Categories;

// ActiveProviders: providers with an ACTIVE account and the category in their professional profile.
// ServicesInProgress: services of the category in the state OPEN or ASSIGNED (RF-F003-03, rule 1).
public record CategoryUsageCount(int ActiveProviders, int ServicesInProgress)
{
    public bool IsInUse => ActiveProviders > 0 || ServicesInProgress > 0;
}