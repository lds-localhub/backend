namespace LocalHub.Domain.Services;

/// <summary>
/// Represents the review of a completed service, made by its client (F012). A service has at most one review.
/// </summary>
public class Review
{
    // Limits of the API documentation (E32), the same as the columns of the data model. The DTOs use the same
    // constants (rule R17).
    public const int CommentMaxLength = 500;
    public const int RatingMin = 1;
    public const int RatingMax = 5;

    public int ReviewId { get; private set; }
    public int ServiceId { get; private set; }
    public short Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Navigation (not a column). "= null!": always set by the Service or loaded by Entity Framework with Include.
    public Service Service { get; private set; } = null!;

    // Private: Entity Framework uses this constructor when it reads a review.
    private Review() { }

    // Only the Service creates its review, so the rules of F012 are always applied. The fields were validated by the
    // DTO; a blank comment arrives as null (rule R17).
    internal Review(short rating, string? comment, DateTimeOffset now)
    {
        Rating = rating;
        Comment = comment?.Trim();
        CreationDate = now;
    }
}
