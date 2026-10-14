namespace LocalHub.Application.Common;

// Page of a list (API documentation, section 4.4): page >= 1 and pageSize from 1 to 50.
public record PageRequest(int Page = PageRequest.DefaultPage, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public int Skip => (Page - 1) * PageSize;

    // Adds the errors of "page" and "pageSize" to the errors of the other fields of the request.
    public void Validate(IDictionary<string, string[]> errors)
    {
        if (Page < 1)
        {
            errors["page"] = ["A página tem de ser um número inteiro igual ou superior a 1."];
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"O tamanho da página tem de estar entre 1 e {MaxPageSize}."];
        }
    }
}