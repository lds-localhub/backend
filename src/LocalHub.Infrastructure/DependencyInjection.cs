using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure.Categories;
using LocalHub.Infrastructure.Geocoding;
using LocalHub.Infrastructure.Security;
using LocalHub.Infrastructure.Seed;

using Microsoft.AspNetCore.Identity;
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
                npgsql.MapEnum<ProposalModel>("proposal_model", nameTranslator: UpperSnakeCaseNameTranslator.Instance);
                npgsql.MapEnum<ServiceStatus>("service_status", nameTranslator: UpperSnakeCaseNameTranslator.Instance);
                npgsql.MapEnum<DirectRequestStatus>("direct_request_status",
                    nameTranslator: UpperSnakeCaseNameTranslator.Instance);
            })
            .UseCamelCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<LocalHubDbContext>();
        services.AddNominatimGeocoding(configuration);
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey tem de ter, no mínimo, 32 caracteres.")
            .ValidateOnStart();
        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<ICategoryUsage, CategoryUsage>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}