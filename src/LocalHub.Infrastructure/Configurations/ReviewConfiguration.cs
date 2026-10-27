using LocalHub.Domain.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalHub.Infrastructure.Configurations;

// Table "Review" of the data model. The 1:0..1 relationship with Service (ON DELETE CASCADE) and the UNIQUE of
// serviceId are found by convention, because Service.Review and Review.Service exist.
public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Review");

        builder.Property(r => r.Comment).HasMaxLength(Review.CommentMaxLength);
    }
}
