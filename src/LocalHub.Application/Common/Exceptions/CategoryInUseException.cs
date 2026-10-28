namespace LocalHub.Application.Common.Exceptions;

// 409 "Categoria em utilização" of E11: the category is still used by active providers or by services
// in progress (RF-F003-03, rule 2). The message says why, so the web application can show it.
public class CategoryInUseException : Exception
{
    public CategoryInUseException(string message)
        : base(message)
    {
    }
}