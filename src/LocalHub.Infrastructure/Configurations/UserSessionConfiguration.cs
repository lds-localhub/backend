using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "UserSession" of the data model. The relationship with User (ON DELETE CASCADE) is found by convention.
public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSession");

        // By convention Entity Framework expects the primary key to be called Id or UserSessionId.
        builder.HasKey(s => s.SessionId);
    }
}
