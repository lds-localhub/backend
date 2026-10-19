using LocalHub.Domain.Profiles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ServiceArea" of the data model: the categories of each profile (RF-F004-04).
// The relationship to ProfessionalProfile (ON DELETE CASCADE) is configured in ProfessionalProfileConfiguration.
public class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.HasKey(a => new { a.ServiceCategoryId, a.ProfessionalProfileId }).HasName("PK_ServiceArea");
        builder.HasIndex(a => a.ProfessionalProfileId).HasDatabaseName("IX_ServiceArea_professionalProfileId");

        builder.HasOne(a => a.ServiceCategory)
            .WithMany()
            .HasForeignKey(a => a.ServiceCategoryId)
            .HasConstraintName("FK_ServiceArea_ServiceCategory")
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("ServiceArea");
    }
}