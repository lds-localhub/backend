namespace LocalHub.Domain.Common;

public enum DomainErrorKind
{
    Validation,
    Forbidden,
    InvalidTransition,
    Conflict,
}