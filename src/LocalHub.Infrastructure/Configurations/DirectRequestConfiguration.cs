using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "DirectRequest" of the data model. The relationship with Service (ON DELETE CASCADE) is found by convention.
public class DirectRequestConfiguration : IEntityTypeConfiguration<DirectRequest>
{
    public void Configure(EntityTypeBuilder<DirectRequest> builder)
    {
        builder.ToTable("DirectRequest");

        builder.HasOne(d => d.ProfessionalProfile)
            .WithMany(p => p.DirectRequests)
            .OnDelete(DeleteBehavior.Restrict);

        // A single direct request per profile and service (RF-F009-01, rule 9).
        builder.HasIndex(d => new { d.ProfessionalProfileId, d.ServiceId }).IsUnique();
    }
}