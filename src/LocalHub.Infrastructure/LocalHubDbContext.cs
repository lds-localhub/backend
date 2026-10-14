using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

public class LocalHubDbContext(DbContextOptions<LocalHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalHubDbContext).Assembly);
    }
}