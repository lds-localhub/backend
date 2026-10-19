using LocalHub.Application.Common.Exceptions;
using LocalHub.Domain.Common;

namespace LocalHub.Application.Common;

// Collects the errors of several fields, so that a single 400 response reports all of them (RNF-08).
public sealed class FieldErrors
{
    private readonly Dictionary<string, string[]> _errors = [];

    public void Add(string field, params string[] messages)
    {
        _errors[field] = messages;
    }

    // Runs a validation of the domain and keeps its message under the field of the exception (or the given field).
    public void Collect(Action validate, string field)
    {
        try
        {
            validate();
        }
        catch (DomainException ex) when (ex.Kind == DomainErrorKind.Validation)
        {
            _errors[ex.Field ?? field] = [ex.Message];
        }
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