using LocalHub.Domain.Services;

namespace LocalHub.Application.Reviews.Dtos;

public record ReviewResponse(int ReviewId, short Rating, string? Comment, DateTimeOffset CreationDate)
{
    public static ReviewResponse From(Review review)
    {
        return new ReviewResponse(review.ReviewId, review.Rating, review.Comment, review.CreationDate);
    }
}

public record ReviewListItem(int ReviewId, short Rating, string? Comment, string ClientName, DateTimeOffset CreationDate);