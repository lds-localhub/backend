namespace LocalHub.Application.Common.Exceptions;

public class CategoryInUseException : Exception
{
    public CategoryInUseException(string message)
        : base(message)
    {
    }
}