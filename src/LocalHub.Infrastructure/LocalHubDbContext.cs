using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure.Locations;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

public class LocalHubDbContext(DbContextOptions<LocalHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ProfessionalProfile> ProfessionalProfiles => Set<ProfessionalProfile>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<DirectRequest> DirectRequests => Set<DirectRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalHubDbContext).Assembly);

        // GeoDistance.HaversineKm, used in the searches by distance (DEC-06).
        GeoDistanceTranslation.Register(modelBuilder);
    }
}