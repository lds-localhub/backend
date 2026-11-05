namespace LocalHub.Domain.Common;

/// <summary>
/// The kind of rule that was broken. The ErrorHandlingMiddleware uses it to choose the HTTP status code.
/// </summary>
public enum DomainErrorKind
{
    Forbidden,
    InvalidTransition,
    Conflict,
}