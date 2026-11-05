namespace LocalHub.Application.Categories;

public record CategoryUsageCount(int ActiveProviders, int ServicesInProgress)
{
    public bool IsInUse => ActiveProviders > 0 || ServicesInProgress > 0;
}