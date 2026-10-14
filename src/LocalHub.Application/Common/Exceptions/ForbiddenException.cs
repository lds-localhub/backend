namespace LocalHub.Application.Common.Exceptions;

// ErrorType is one of ErrorTypes (conta-desativada, perfil-em-falta) when the frontend has to tell the cases apart.
public class ForbiddenException(string message, string? errorType = null) : Exception(message)
{
    public string? ErrorType { get; } = errorType;
}