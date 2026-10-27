namespace LocalHub.Application.Common.Exceptions;

// ErrorType is one of ErrorTypes (conta-desativada, perfil-em-falta) when the frontend has to tell the cases apart.
public class ForbiddenException : Exception
{
    public ForbiddenException(string message, string? errorType = null)
        : base(message)
    {
        ErrorType = errorType;
    }

    public string? ErrorType { get; }
}