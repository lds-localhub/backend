using LocalHub.Application.Common.Exceptions;

namespace LocalHub.Application.Common;

// Collects the errors of several fields, so that a single 400 response reports all of them (RNF-08).
public sealed class FieldErrors
{
    private readonly Dictionary<string, string[]> _errors = new Dictionary<string, string[]>();

    public void Add(string field, params string[] messages)
    {
        _errors[field] = messages;
    }

    public bool Contains(string field)
    {
        return _errors.ContainsKey(field);
    }

    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>(_errors));
        }
    }
}
