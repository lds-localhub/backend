using LocalHub.Application;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DependencyInjection = LocalHub.Infrastructure.DependencyInjection;

namespace LocalHub.UnitTests;

[Trait("Category", "Unit")]
public class DependencyInjectionTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=test;Username=test;Password=test";

    [Fact]
    public void AddApplication_ConfiguracaoValida_RegistaLocalHubDbContextComPostgreSql()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = ConnectionString
            })
            .Build();
        ServiceCollection services = new();

        services.AddApplication(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        LocalHubDbContext db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        Assert.Equal("Host=localhost;Port=5432;Database=test;Username=test", db.Database.GetConnectionString());
    }
}