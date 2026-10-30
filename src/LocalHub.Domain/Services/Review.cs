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

    // Error type of the API (section 4.6) for a second review of the same service (E32; RF-F012-01, rule 3).
    public const string AlreadyExistsErrorType = "avaliacao-existente";

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
    // DTO; a blank comment is saved as null (API documentation, E32).
    internal Review(Service service, short rating, string? comment, DateTimeOffset now)
    {
        Service = service;
        ServiceId = service.ServiceId;
        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CreationDate = now;
    }
}