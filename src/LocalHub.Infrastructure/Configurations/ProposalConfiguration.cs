using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "Proposal" of the data model: the proposal of a provider for a service (F007, F011).
// The relationship to Service (ON DELETE CASCADE) is configured in ServiceConfiguration.
public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public const string AcceptedIndexName = "UX_Proposal_serviceId_accepted";

    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.HasKey(p => p.ProposalId).HasName("PK_Proposal");
        builder.Property(p => p.ProposalId).UseIdentityAlwaysColumn();
        builder.Property(p => p.Amount).HasPrecision(8, 2);
        builder.Property(p => p.Conditions).HasMaxLength(Proposal.ConditionsMaxLength);
        builder.Property(p => p.Obs).HasMaxLength(Proposal.ObsMaxLength);

        builder.HasOne(p => p.ProfessionalProfile)
            .WithMany()
            .HasForeignKey(p => p.ProfessionalProfileId)
            .HasConstraintName("FK_Proposal_ProfessionalProfile")
            .OnDelete(DeleteBehavior.Restrict);

        // A single proposal per provider and service, whatever its state (RF-F011-02, rule 6).
        builder.HasIndex(p => new { p.ProfessionalProfileId, p.ServiceId })
            .IsUnique()
            .HasDatabaseName("UQ_Proposal_professionalProfileId_serviceId");
        builder.HasIndex(p => p.ServiceId).HasDatabaseName("IX_Proposal_serviceId");
        // At most one accepted proposal per service, even with simultaneous requests (Class Model, rule R1a).
        builder.HasIndex(p => p.ServiceId, AcceptedIndexName)
            .IsUnique()
            .HasDatabaseName(AcceptedIndexName)
            .HasFilter("\"status\" = 'ACCEPTED'");

        // xmin (Class Model, rule R1b): accepting and declining the same proposal at the same time cannot both be saved.
        builder.Property<uint>(ServiceConfiguration.VersionProperty).IsRowVersion();

        builder.ToTable("Proposal", t =>
        {
            t.HasCheckConstraint("CK_Proposal_amount", "\"amount\" > 0 AND \"amount\" <= 100000");
            t.HasCheckConstraint("CK_Proposal_conditions", "btrim(\"conditions\") <> ''");
            t.HasCheckConstraint("CK_Proposal_obs", "\"obs\" IS NULL OR btrim(\"obs\") <> ''");
            // Decision date null while PENDING and required in the other states.
            t.HasCheckConstraint("CK_Proposal_decisionDate",
                "(\"status\" = 'PENDING') = (\"decisionDate\" IS NULL) "
                + "AND (\"decisionDate\" IS NULL OR \"decisionDate\" >= \"creationDate\")");
        });
    }
}