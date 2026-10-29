using LocalHub.Domain.Services;

namespace LocalHub.Application.Reviews.Dtos;

// Review created by E32 (API documentation, F012).
public record ReviewResponse(int ReviewId, short Rating, string? Comment, DateTimeOffset CreationDate)
{
    public static ReviewResponse From(Review review)
    {
        return new ReviewResponse(review.ReviewId, review.Rating, review.Comment, review.CreationDate);
    }
}

// Item of the list of E15: the review and the name of the client that made it.
public record ReviewListItem(int ReviewId, short Rating, string? Comment, string ClientName, DateTimeOffset CreationDate);
