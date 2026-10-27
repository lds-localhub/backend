using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ServiceStatusHistory" of the data model. The relationship with Service (ON DELETE CASCADE) is found by
// convention.
public class ServiceStatusHistoryConfiguration : IEntityTypeConfiguration<ServiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<ServiceStatusHistory> builder)
    {
        builder.ToTable("ServiceStatusHistory");

        // The user that changed the state is kept even if the account is deactivated.
        builder.HasOne(h => h.ChangedBy)
            .WithMany(u => u.StatusChanges)
            .OnDelete(DeleteBehavior.Restrict);
    }
}