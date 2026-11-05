using LocalHub.Domain.Profiles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("ServiceArea");

        builder.HasKey(a => new { a.ServiceCategoryId, a.ProfessionalProfileId });

        // Categories are never deleted, only deactivated.
        builder.HasOne(a => a.ServiceCategory)
            .WithMany(c => c.ServiceAreas)
            .OnDelete(DeleteBehavior.Restrict);
    }
}