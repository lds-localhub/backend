using LocalHub.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "User" of the data model. The relationships with UserSession, ProfessionalProfile, Service and
// ServiceStatusHistory are configured in the classes of those entities or found by convention.
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

        // The e-mail is always saved in lowercase (User.Register), so a normal UNIQUE is enough.
        // The name is the one the AuthService looks for in the catch.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UX_User_email");
    }
}
