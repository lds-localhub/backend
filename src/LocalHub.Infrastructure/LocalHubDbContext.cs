using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

public class LocalHubDbContext : DbContext
{
    public LocalHubDbContext(DbContextOptions<LocalHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ProfessionalProfile> ProfessionalProfiles => Set<ProfessionalProfile>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceStatusHistory> ServiceStatusHistories => Set<ServiceStatusHistory>();
    public DbSet<DirectRequest> DirectRequests => Set<DirectRequest>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseIdentityAlwaysColumns();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalHubDbContext).Assembly);
    }
}