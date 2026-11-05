using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Geocoding;
using LocalHub.Infrastructure.Seed;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql;

namespace LocalHub.IntegrationTests.Fixtures;

// Runs the API against localhub_test, emptied, migrated and seeded once per run.
public class LocalHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SeedPassword = "Teste2026";

    private const string TestDatabaseName = "localhub_test";
    private const string SigningKey = "integration-tests-signing-key-not-a-secret";

    private readonly string _connectionString = BuildConnectionString();

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();

        // Removes every table and type so that the migrations always start from an empty database.
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");
        await db.Database.MigrateAsync();

        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
    }

    // Explicit because WebApplicationFactory already has a DisposeAsync with another return type.
    Task IAsyncLifetime.DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" does not load appsettings.Development.json, so the development database is never used.
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", _connectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Seed:DefaultPassword", SeedPassword);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeocodingService>();
            services.AddSingleton<IGeocodingService, FakeGeocodingService>();
            services.AddHttpClient(GeocodingServiceCollectionExtensions.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new FailIfCalledHandler());
        });
    }

    // CI: ConnectionStrings__LocalHub. Locally: appsettings.Development.json. The database is always localhub_test.
    private static string BuildConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LocalHub");

        if (string.IsNullOrEmpty(connectionString))
        {
            string? settingsPath = FindDevelopmentSettings();
            if (settingsPath != null)
            {
                IConfiguration settings = new ConfigurationBuilder().AddJsonFile(settingsPath).Build();
                connectionString = settings.GetConnectionString(DependencyInjection.ConnectionStringName);
            }
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Defina ConnectionStrings__LocalHub ou crie src/LocalHub.Api/appsettings.Development.json.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Database = TestDatabaseName;
        return builder.ConnectionString;
    }

    private static string? FindDevelopmentSettings()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(directory.FullName, "src", "LocalHub.Api", "appsettings.Development.json");
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        return null;
    }
}