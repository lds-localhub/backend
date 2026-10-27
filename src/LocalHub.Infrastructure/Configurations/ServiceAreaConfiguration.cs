using LocalHub.Domain.Profiles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ServiceArea" of the data model: the categories of each profile (RF-F004-04). The relationship with
// ProfessionalProfile (ON DELETE CASCADE) is found by convention.
public class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("ServiceArea");

        builder.HasKey(a => new { a.ServiceCategoryId, a.ProfessionalProfileId });

        // The categories are never deleted, only deactivated.
        builder.HasOne(a => a.ServiceCategory)
            .WithMany(c => c.ServiceAreas)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
