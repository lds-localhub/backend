namespace LocalHub.Domain;


public enum DomainErrorKind
{
    Validation,
    Forbidden,
    InvalidTransition,
    Conflict,
}

public sealed class DomainException : Exception
{
    public DomainException()
        : this(DomainErrorKind.Validation, "A operação viola uma regra de negócio.")
    {
    }

    public DomainException(string message)
        : this(DomainErrorKind.Validation, message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = DomainErrorKind.Validation;
    }

    public DomainException(DomainErrorKind kind, string message, string? field = null)
        : base(message)
    {
        Kind = kind;
        Field = field;
    }

    public DomainErrorKind Kind { get; }

    public string? Field { get; }
}