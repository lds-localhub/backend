using LocalHub.Domain.Profiles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class ProfessionalProfileConfiguration : IEntityTypeConfiguration<ProfessionalProfile>
{
    public void Configure(EntityTypeBuilder<ProfessionalProfile> builder)
    {
        builder.ToTable("ProfessionalProfile");

        builder.Property(p => p.Introduction).HasMaxLength(ProfessionalProfile.IntroductionMaxLength);
        builder.Property(p => p.ContactDetails).HasMaxLength(ProfessionalProfile.ContactDetailsMaxLength);
        builder.Property(p => p.Experience).HasMaxLength(ProfessionalProfile.ExperienceMaxLength);
        builder.Property(p => p.AverageRating).HasPrecision(2, 1);

        builder.HasOne(p => p.ServiceProvider)
            .WithOne(u => u.Profile)
            .HasForeignKey<ProfessionalProfile>(p => p.ServiceProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}