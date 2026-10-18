using LocalHub.Domain.Categories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "ServiceCategory" of the data model (database/01-esquema.sql). The unique index UX_ServiceCategory_name,
// on lower("name"), is created by the migration AddServiceCategories, because EF Core cannot describe it.
public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.HasKey(c => c.ServiceCategoryId).HasName("PK_ServiceCategory");
        builder.Property(c => c.ServiceCategoryId).UseIdentityAlwaysColumn();
        builder.Property(c => c.Name).HasMaxLength(ServiceCategory.NameMaxLength);

        builder.ToTable("ServiceCategory", t =>
        {
            t.HasCheckConstraint("CK_ServiceCategory_name", "btrim(\"name\") <> ''");
        });
    }
}