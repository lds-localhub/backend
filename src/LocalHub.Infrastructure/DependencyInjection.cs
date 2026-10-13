using LocalHub.Domain.Accounts;
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
            })
            .UseCamelCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<LocalHubDbContext>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey tem de ter, no mínimo, 32 caracteres.")
            .ValidateOnStart();
        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}