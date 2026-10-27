using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure.Locations;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

// Each table is configured with the Fluent API in its own class of the folder Configurations (UserConfiguration, ...).
// The relationships with ON DELETE CASCADE and the foreign keys are found by the conventions of Entity Framework,
// from the navigations of the classes of the domain. There are no CHECK constraints: each rule is checked by the
// DTOs, the services or the domain (rule R17).
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
        // GENERATED ALWAYS AS IDENTITY in every simple primary key.
        modelBuilder.UseIdentityAlwaysColumns();

        // Every class of this project that implements IEntityTypeConfiguration (folder Configurations).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalHubDbContext).Assembly);

        // GeoDistance.HaversineKm, used in the searches by distance (DEC-06).
        GeoDistanceTranslation.Register(modelBuilder);
    }
}