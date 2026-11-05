namespace LocalHub.Domain.Services;

/// <summary>
/// The review of a completed service, made by its client. A service has at most one review.
/// </summary>
public class Review
{
    public const int CommentMaxLength = 500;
    public const int RatingMin = 1;
    public const int RatingMax = 5;

    public const string AlreadyExistsErrorType = "avaliacao-existente";

    public int ReviewId { get; private set; }
    public int ServiceId { get; private set; }
    public short Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreationDate { get; private set; }

    // Navigation: set by the Service or loaded by Entity Framework with Include.
    public Service Service { get; private set; } = null!;

    // Used by Entity Framework; a review is created by Service.AddReview.
    private Review() { }

    internal Review(Service service, short rating, string? comment, DateTimeOffset now)
    {
        Service = service;
        ServiceId = service.ServiceId;
        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CreationDate = now;
    }
}