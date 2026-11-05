namespace LocalHub.Domain.Common;

/// <summary>
/// Thrown when a business rule of the domain is broken.
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
    public string? Field { get; }
    public string? ErrorType { get; }
}