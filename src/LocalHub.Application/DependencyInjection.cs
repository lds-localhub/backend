using LocalHub.Application.Accounts;
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

        return services;
    }
}