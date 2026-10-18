using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

public class LocalHubDbContext(DbContextOptions<LocalHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalHubDbContext).Assembly);
    }
}