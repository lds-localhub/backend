namespace LocalHub.Application.Common.Exceptions;

public class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Corrija os campos assinalados.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}