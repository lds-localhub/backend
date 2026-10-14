namespace LocalHub.Domain.Common;

/// <summary>
/// Represents an error thrown when a business rule of the domain is broken.
/// </summary>
public class DomainException(DomainErrorKind kind, string message, string? field = null) : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;
    // Name of the request field that caused the error (for example "email"), so the API can show the message
    // next to that field. It is null when the error is not about a specific field.
    public string? Field { get; } = field;
}