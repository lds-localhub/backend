using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "Service" of the data model (database/01-esquema.sql): the services with direct requests and the published
// jobs (F009, F010).
public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    // Shadow property mapped to the PostgreSQL system column xmin (Class Model, rule R1): a service changed by another
    // request after being read is not saved (DbUpdateConcurrencyException).
    public const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(s => s.ServiceId).HasName("PK_Service");
        builder.Property(s => s.ServiceId).UseIdentityAlwaysColumn();
        builder.Property(s => s.Description).HasMaxLength(Service.DescriptionMaxLength);
        builder.Property(s => s.Location).HasMaxLength(Service.LocationMaxLength);
        builder.Property(s => s.Latitude).HasPrecision(8, 6);
        builder.Property(s => s.Longitude).HasPrecision(9, 6);
        builder.Property<uint>(VersionProperty).IsRowVersion();

        // The accounts and the categories are never deleted, only deactivated.
        builder.HasOne(s => s.Client)
            .WithMany()
            .HasForeignKey(s => s.ClientId)
            .HasConstraintName("FK_Service_User")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.ClientId).HasDatabaseName("IX_Service_clientId");

        builder.HasOne(s => s.ServiceCategory)
            .WithMany()
            .HasForeignKey(s => s.ServiceCategoryId)
            .HasConstraintName("FK_Service_ServiceCategory")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.ServiceCategoryId, s.Status })
            .HasDatabaseName("IX_Service_serviceCategoryId_status");

        builder.HasMany(s => s.DirectRequests)
            .WithOne()
            .HasForeignKey(r => r.ServiceId)
            .HasConstraintName("FK_DirectRequest_Service")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.DirectRequests).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable("Service", t =>
        {
            t.HasCheckConstraint("CK_Service_description", "btrim(\"description\") <> ''");
            // Requested date equal to or after the creation date (civil date in Portugal).
            t.HasCheckConstraint("CK_Service_requestedStartDate",
                "\"requestedStartDate\" >= (\"creationDate\" AT TIME ZONE 'Europe/Lisbon')::date");
            t.HasCheckConstraint("CK_Service_requestedEndDate",
                "\"requestedEndDate\" IS NULL OR \"requestedEndDate\" >= \"requestedStartDate\"");
            t.HasCheckConstraint("CK_Service_location", "btrim(\"location\") <> ''");
            t.HasCheckConstraint("CK_Service_latitude", "\"latitude\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_Service_longitude", "\"longitude\" BETWEEN -180 AND 180");
            // Completion date required only in the state COMPLETED and null in the other states.
            t.HasCheckConstraint("CK_Service_actualCompletionDate",
                "(\"status\" = 'COMPLETED') = (\"actualCompletionDate\" IS NOT NULL) "
                + "AND (\"actualCompletionDate\" IS NULL OR \"actualCompletionDate\" >= \"creationDate\")");
        });
    }
}