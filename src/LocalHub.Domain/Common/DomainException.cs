namespace LocalHub.Domain.Common;

/// <summary>
/// Represents an error thrown when a business rule of the domain is broken.
/// </summary>
public class DomainException : Exception
{
    public DomainException(DomainErrorKind kind, string message, string? field = null, string? errorType = null)
        : base(message)
    {
        Kind = kind;
        Field = field;
        ErrorType = errorType;
    }

    public DomainErrorKind Kind { get; }
    // Name of the request field that caused the error (for example "email"), so the API can show the message
    // next to that field. It is null when the error is not about a specific field.
    public string? Field { get; }
    // Error type of the API (section 4.6, for example "avaliacao-existente") when the frontend has to tell the case
    // apart from other errors with the same status code; null for the default type.
    public string? ErrorType { get; }
}