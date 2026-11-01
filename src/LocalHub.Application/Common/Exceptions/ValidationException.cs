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

    // A global error of the operation, not tied to a field: the response has only "detail" (API documentation, 4.6).
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    // Empty for a global error of the operation.
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}