using LocalHub.Domain.Categories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ServiceCategory" of the data model.
public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ToTable("ServiceCategory");

        builder.Property(c => c.Name).HasMaxLength(ServiceCategory.NameMaxLength);

        // Normal UNIQUE; the uniqueness ignoring upper and lower case is checked by the CategoryService (E09, E10).
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("UX_ServiceCategory_name");
    }
}