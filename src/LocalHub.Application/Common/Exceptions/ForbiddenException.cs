namespace LocalHub.Application.Common.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message, string? errorType = null)
        : base(message)
    {
        ErrorType = errorType;
    }

    public string? ErrorType { get; }
}