using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalHub.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "LocalHub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);

        services.AddDbContext<LocalHubDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MapEnum<UserRole>("user_role", nameTranslator: UpperSnakeCaseNameTranslator.Instance);
                npgsql.MapEnum<ActivationStatus>("activation_status",
                    nameTranslator: UpperSnakeCaseNameTranslator.Instance);
            })
            .UseCamelCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<LocalHubDbContext>();

        return services;
    }
}