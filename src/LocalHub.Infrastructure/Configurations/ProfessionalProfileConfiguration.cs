using LocalHub.Domain.Profiles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ProfessionalProfile" of the data model. The relationships with ServiceArea, DirectRequest and Proposal are
// configured in the classes of those entities or found by convention.
public class ProfessionalProfileConfiguration : IEntityTypeConfiguration<ProfessionalProfile>
{
    public void Configure(EntityTypeBuilder<ProfessionalProfile> builder)
    {
        builder.ToTable("ProfessionalProfile");

        builder.Property(p => p.Introduction).HasMaxLength(ProfessionalProfile.IntroductionMaxLength);
        builder.Property(p => p.ContactDetails).HasMaxLength(ProfessionalProfile.ContactDetailsMaxLength);
        builder.Property(p => p.Experience).HasMaxLength(ProfessionalProfile.ExperienceMaxLength);
        builder.Property(p => p.AverageRating).HasPrecision(2, 1);

        // 1:0..1 with the provider; the UNIQUE of serviceProviderId is created by the 1:1 relationship.
        // The accounts are never deleted, only deactivated.
        builder.HasOne(p => p.ServiceProvider)
            .WithOne(u => u.Profile)
            .HasForeignKey<ProfessionalProfile>(p => p.ServiceProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
