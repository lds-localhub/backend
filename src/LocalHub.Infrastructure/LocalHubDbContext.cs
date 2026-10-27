using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure.Locations;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Infrastructure;

// The tables, the columns, the sizes, the relationships and the foreign keys come from the classes of the domain
// (properties, navigations and attributes, found by the conventions of Entity Framework). OnModelCreating only has
// what has no .NET attribute: identity columns, the composite key, the unique indexes and ON DELETE RESTRICT.
// There are no CHECK constraints: each rule is checked by the DTOs, the services or the domain (rule R17).
public class LocalHubDbContext(DbContextOptions<LocalHubDbContext> options) : DbContext(options)
{
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

        modelBuilder.Entity<User>(b =>
        {
            // The e-mail is always saved in lowercase (User.Register), so a normal UNIQUE is enough.
            // The name is the one the AuthService looks for in the catch.
            b.HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("UX_User_email");
        });

        modelBuilder.Entity<ProfessionalProfile>(b =>
        {
            // 1:0..1 with the provider; the UNIQUE of serviceProviderId is created by the 1:1 relationship.
            b.HasOne(p => p.ServiceProvider)
                .WithOne(u => u.Profile)
                .HasForeignKey<ProfessionalProfile>(p => p.ServiceProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceCategory>(b =>
        {
            // Normal UNIQUE; the uniqueness ignoring upper and lower case is checked by the CategoryService (E09, E10).
            b.HasIndex(c => c.Name)
                .IsUnique()
                .HasDatabaseName("UX_ServiceCategory_name");
        });

        modelBuilder.Entity<ServiceArea>(b =>
        {
            b.HasKey(a => new { a.ServiceCategoryId, a.ProfessionalProfileId });

            b.HasOne(a => a.ServiceCategory)
                .WithMany(c => c.ServiceAreas)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Service>(b =>
        {
            b.HasOne(s => s.Client)
                .WithMany(u => u.Services)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(s => s.ServiceCategory)
                .WithMany(c => c.Services)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceStatusHistory>(b =>
        {
            b.HasOne(h => h.ChangedBy)
                .WithMany(u => u.StatusChanges)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DirectRequest>(b =>
        {
            b.HasOne(d => d.ProfessionalProfile)
                .WithMany(p => p.DirectRequests)
                .OnDelete(DeleteBehavior.Restrict);

            // A single direct request per profile and service (RF-F009-01, rule 9).
            b.HasIndex(d => new { d.ProfessionalProfileId, d.ServiceId }).IsUnique();
        });

        modelBuilder.Entity<Proposal>(b =>
        {
            b.HasOne(p => p.ProfessionalProfile)
                .WithMany(pp => pp.Proposals)
                .OnDelete(DeleteBehavior.Restrict);

            // A single proposal per provider and service, whatever its state (RF-F011-02, rule 6).
            b.HasIndex(p => new { p.ProfessionalProfileId, p.ServiceId }).IsUnique();

            // Rule R1: at most one accepted proposal per service.
            b.HasIndex(p => p.ServiceId)
                .IsUnique()
                .HasFilter("\"status\" = 'ACCEPTED'")
                .HasDatabaseName("UX_Proposal_serviceId_accepted");
        });

        // GeoDistance.HaversineKm, used in the searches by distance (DEC-06).
        GeoDistanceTranslation.Register(modelBuilder);
    }
}
