using LocalHub.Application.Accounts;
using LocalHub.Application.Categories;
using LocalHub.Application.Locations;
using LocalHub.Application.Profiles;
using LocalHub.Application.Search;
using LocalHub.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LocalHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<LocationResolver>();
        services.AddScoped<LocationService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<SearchService>();

        return services;
    }
}