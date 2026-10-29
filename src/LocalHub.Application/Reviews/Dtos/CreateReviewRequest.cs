using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Domain.Services;

namespace LocalHub.Application.Reviews.Dtos;

// Body of E32. Fields that are not listed here are rejected with 400. A rating with decimal places (3.5) is not a
// whole number, so the JSON reader rejects it with 400.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateReviewRequest(
    [Required(ErrorMessage = "Indique a classificação.")]
    [Range(Review.RatingMin, Review.RatingMax, ErrorMessage = "A classificação tem de ser um número inteiro de 1 a 5.")]
    short? Rating,

    [StringLength(Review.CommentMaxLength, ErrorMessage = "O comentário tem no máximo 500 caracteres.")]
    string? Comment);
