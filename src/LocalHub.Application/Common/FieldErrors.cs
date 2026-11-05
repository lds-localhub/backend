using LocalHub.Application.Common.Exceptions;

namespace LocalHub.Application.Common;

public sealed class FieldErrors
{
    private readonly Dictionary<string, string[]> _errors = new();

    public void Add(string field, params string[] messages)
    {
        _errors[field] = messages;
    }

    public bool Contains(string field) => _errors.ContainsKey(field);

    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>(_errors));
        }
    }
}