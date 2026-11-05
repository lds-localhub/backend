using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("Proposal");

        builder.Property(p => p.Conditions).HasMaxLength(Proposal.ConditionsMaxLength);
        builder.Property(p => p.Obs).HasMaxLength(Proposal.ObsMaxLength);
        builder.Property(p => p.Amount).HasPrecision(8, 2);

        builder.HasOne(p => p.ProfessionalProfile)
            .WithMany(pp => pp.Proposals)
            .OnDelete(DeleteBehavior.Restrict);

        // One proposal per provider and service.
        builder.HasIndex(p => new { p.ProfessionalProfileId, p.ServiceId }).IsUnique();

        // At most one accepted proposal per service.
        builder.HasIndex(p => p.ServiceId)
            .IsUnique()
            .HasFilter("\"status\" = 'ACCEPTED'")
            .HasDatabaseName("UX_Proposal_serviceId_accepted");
    }
}