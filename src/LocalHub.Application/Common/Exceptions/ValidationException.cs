namespace LocalHub.Application.Common.Exceptions;

// Thrown by the services of the application when a value of the request does not make sense in the backend (rule R17):
// a category that does not exist, an address that is not found, an end date before the start date, ...
public class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Corrija os campos assinalados.")
    {
        Errors = errors;
    }

    // A single field with a single message.
    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
