namespace LocalHub.Domain.Common;

/// <summary>
/// Represents the kind of rule that was broken. The ErrorHandlingMiddleware uses it to choose the HTTP status code.
/// The domain only checks the state and the actor of the operations; the fields of the requests are validated by the
/// DTOs and by the services of the application (rule R17).
/// </summary>
public enum DomainErrorKind
{
    Forbidden,
    InvalidTransition,
    Conflict,
}