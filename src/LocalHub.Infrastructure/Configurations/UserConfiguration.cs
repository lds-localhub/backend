using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.UserId).HasName("PK_User");
        builder.Property(u => u.UserId).UseIdentityAlwaysColumn();
        builder.Property(u => u.Name).HasMaxLength(100);
        builder.Property(u => u.Email).HasMaxLength(254);
        builder.Property(u => u.PasswordHash).HasMaxLength(255);
        builder.Property(u => u.Address).HasMaxLength(255);
        builder.Property(u => u.Latitude).HasPrecision(8, 6);
        builder.Property(u => u.Longitude).HasPrecision(9, 6);

        builder.ToTable("User", t =>
        {
            t.HasCheckConstraint("CK_User_name", "btrim(\"name\") <> ''");
            t.HasCheckConstraint("CK_User_latitude", "\"latitude\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_User_longitude", "\"longitude\" BETWEEN -180 AND 180");
            t.HasCheckConstraint("CK_User_coordinates", "(\"latitude\" IS NULL) = (\"longitude\" IS NULL)");
        });
    }
}