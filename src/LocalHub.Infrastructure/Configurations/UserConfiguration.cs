using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");

        builder.Property(u => u.Name).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(User.PasswordHashMaxLength);
        builder.Property(u => u.Address).HasMaxLength(User.AddressMaxLength);
        builder.Property(u => u.Latitude).HasPrecision(8, 6);
        builder.Property(u => u.Longitude).HasPrecision(9, 6);

        // E-mails are stored in lowercase, so a normal unique index is enough.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UX_User_email");
    }
}