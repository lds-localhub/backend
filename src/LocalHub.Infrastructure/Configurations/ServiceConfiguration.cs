using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "Service" of the data model. The relationships with DirectRequest, Proposal, Review and ServiceStatusHistory
// (ON DELETE CASCADE) are found by convention.
public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Service");

        builder.Property(s => s.Title).HasMaxLength(Service.TitleMaxLength);
        builder.Property(s => s.Description).HasMaxLength(Service.DescriptionMaxLength);
        builder.Property(s => s.Location).HasMaxLength(Service.LocationMaxLength);
        builder.Property(s => s.Latitude).HasPrecision(8, 6);
        builder.Property(s => s.Longitude).HasPrecision(9, 6);

        // The accounts and the categories are never deleted, only deactivated.
        builder.HasOne(s => s.Client)
            .WithMany(u => u.Services)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ServiceCategory)
            .WithMany(c => c.Services)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
