namespace LocalHub.Domain.Common;

public class DomainException(DomainErrorKind kind, string message, string? field = null) : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;
    public string? Field { get; } = field;
}