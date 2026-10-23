using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "DirectRequest" of the data model: a request for a proposal sent to a chosen provider (F009).
// The relationship to Service (ON DELETE CASCADE) is configured in ServiceConfiguration.
public class DirectRequestConfiguration : IEntityTypeConfiguration<DirectRequest>
{
    public void Configure(EntityTypeBuilder<DirectRequest> builder)
    {
        builder.HasKey(r => r.DirectRequestId).HasName("PK_DirectRequest");
        builder.Property(r => r.DirectRequestId).UseIdentityAlwaysColumn();

        builder.HasOne(r => r.ProfessionalProfile)
            .WithMany()
            .HasForeignKey(r => r.ProfessionalProfileId)
            .HasConstraintName("FK_DirectRequest_ProfessionalProfile")
            .OnDelete(DeleteBehavior.Restrict);

        // A single direct request per profile and service (RF-F009-01, rule 9).
        builder.HasIndex(r => new { r.ProfessionalProfileId, r.ServiceId })
            .IsUnique()
            .HasDatabaseName("UQ_DirectRequest_professionalProfileId_serviceId");
        builder.HasIndex(r => r.ServiceId).HasDatabaseName("IX_DirectRequest_serviceId");

        builder.ToTable("DirectRequest", t =>
        {
            // Response date required for PROPOSAL_SUBMITTED and DECLINED; null for PENDING and VOID.
            t.HasCheckConstraint("CK_DirectRequest_responseDate",
                "(\"status\" IN ('PROPOSAL_SUBMITTED', 'DECLINED')) = (\"responseDate\" IS NOT NULL) "
                + "AND (\"responseDate\" IS NULL OR \"responseDate\" >= \"creationDate\")");
        });
    }
}