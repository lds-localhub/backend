using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Seed;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace LocalHub.IntegrationTests.Fixtures;

// Starts the API against the PostgreSQL test database "localhub_test".
// The database is emptied, migrated and seeded once per test run.
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

    // Written as "IAsyncLifetime.DisposeAsync" because WebApplicationFactory already has a DisposeAsync method
    // with a different return type. There is nothing to clean up at the end.
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
    }

    // CI: ConnectionStrings__LocalHub (ci.yml). Locally: the connection string of appsettings.Development.json.
    // The database name is always replaced by localhub_test.
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
        // Starts in the test's bin folder and goes up one folder at a time until it finds the backend folder.
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