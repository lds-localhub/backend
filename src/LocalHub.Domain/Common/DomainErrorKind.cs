namespace LocalHub.Domain.Common;

/// <summary>
/// Represents the kind of rule that was broken. The ErrorHandlingMiddleware uses it to choose the HTTP status code.
/// </summary>
public enum DomainErrorKind
{
    Validation,
    Forbidden,
    InvalidTransition,
    Conflict,
}