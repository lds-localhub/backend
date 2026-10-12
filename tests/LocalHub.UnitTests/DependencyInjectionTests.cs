using LocalHub.Application;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalHub.UnitTests;

public class DependencyInjectionTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=test;Username=test;Password=test";

    [Fact]
    public void AddApplication_RegistersLocalHubDbContextWithPostgreSql()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{Infrastructure.DependencyInjection.ConnectionStringName}"] = ConnectionString
            })
            .Build();
        var services = new ServiceCollection();

        services.AddApplication(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        Assert.Equal(ConnectionString, db.Database.GetConnectionString());
    }
}