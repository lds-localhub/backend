using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(s => s.SessionId).HasName("PK_UserSession");
        builder.Property(s => s.SessionId).UseIdentityAlwaysColumn();

        builder.HasOne<User>()
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("FK_UserSession_User")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.UserId).HasDatabaseName("IX_UserSession_userId");

        builder.ToTable("UserSession", t =>
        {
            t.HasCheckConstraint("CK_UserSession_expiresAt",
                "\"expiresAt\" > \"createdAt\" AND \"expiresAt\" <= \"createdAt\" + interval '8 hours'");
            t.HasCheckConstraint("CK_UserSession_revokedAt",
                "\"revokedAt\" IS NULL OR \"revokedAt\" >= \"createdAt\"");
        });
    }
}