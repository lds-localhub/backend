using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class ServiceStatusHistoryConfiguration : IEntityTypeConfiguration<ServiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<ServiceStatusHistory> builder)
    {
        builder.ToTable("ServiceStatusHistory");

        // Keep the author even if the account is deactivated.
        builder.HasOne(h => h.ChangedBy)
            .WithMany(u => u.StatusChanges)
            .OnDelete(DeleteBehavior.Restrict);
    }
}