using LocalHub.Domain.Profiles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ProfessionalProfile" of the data model (database/01-esquema.sql).
public class ProfessionalProfileConfiguration : IEntityTypeConfiguration<ProfessionalProfile>
{
    public void Configure(EntityTypeBuilder<ProfessionalProfile> builder)
    {
        builder.HasKey(p => p.ProfessionalProfileId).HasName("PK_ProfessionalProfile");
        builder.Property(p => p.ProfessionalProfileId).UseIdentityAlwaysColumn();
        builder.Property(p => p.Introduction).HasMaxLength(ProfessionalProfile.IntroductionMaxLength);
        builder.Property(p => p.ContactDetails).HasMaxLength(ProfessionalProfile.ContactDetailsMaxLength);
        builder.Property(p => p.Experience).HasMaxLength(ProfessionalProfile.ExperienceMaxLength);
        builder.Property(p => p.AverageRating).HasPrecision(2, 1);

        // At most one profile per provider (RF-F004-01, rule 1). The accounts are never deleted, only deactivated.
        builder.HasOne(p => p.ServiceProvider)
            .WithOne()
            .HasForeignKey<ProfessionalProfile>(p => p.ServiceProviderId)
            .HasConstraintName("FK_ProfessionalProfile_User")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.ServiceProviderId)
            .IsUnique()
            .HasDatabaseName("UQ_ProfessionalProfile_serviceProviderId");

        builder.HasMany(p => p.ServiceAreas)
            .WithOne()
            .HasForeignKey(a => a.ProfessionalProfileId)
            .HasConstraintName("FK_ServiceArea_ProfessionalProfile")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.ServiceAreas).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable("ProfessionalProfile", t =>
        {
            t.HasCheckConstraint("CK_ProfessionalProfile_introduction", "btrim(\"introduction\") <> ''");
            t.HasCheckConstraint("CK_ProfessionalProfile_contactDetails", "btrim(\"contactDetails\") <> ''");
            t.HasCheckConstraint("CK_ProfessionalProfile_experience", "btrim(\"experience\") <> ''");
            t.HasCheckConstraint("CK_ProfessionalProfile_averageRating", "\"averageRating\" BETWEEN 1.0 AND 5.0");
            t.HasCheckConstraint("CK_ProfessionalProfile_serviceRadiusKm", "\"serviceRadiusKm\" BETWEEN 1 AND 100");
        });
    }
}