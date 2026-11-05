using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class DirectRequestConfiguration : IEntityTypeConfiguration<DirectRequest>
{
    public void Configure(EntityTypeBuilder<DirectRequest> builder)
    {
        builder.ToTable("DirectRequest");

        builder.HasOne(d => d.ProfessionalProfile)
            .WithMany(p => p.DirectRequests)
            .OnDelete(DeleteBehavior.Restrict);

        // One direct request per profile and service.
        builder.HasIndex(d => new { d.ProfessionalProfileId, d.ServiceId }).IsUnique();
    }
}