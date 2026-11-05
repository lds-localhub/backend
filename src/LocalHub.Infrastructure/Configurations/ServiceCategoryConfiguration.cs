using LocalHub.Domain.Categories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ToTable("ServiceCategory");

        builder.Property(c => c.Name).HasMaxLength(ServiceCategory.NameMaxLength);

        // Case-insensitive uniqueness is checked by CategoryService.
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("UX_ServiceCategory_name");
    }
}