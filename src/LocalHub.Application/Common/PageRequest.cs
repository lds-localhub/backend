namespace LocalHub.Application.Common;

// Page of a list (API documentation, section 4.4): page >= 1 and pageSize from 1 to 50.
public record PageRequest(int Page = PageRequest.DefaultPage, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    // Calculated in long so that a very large page (for example int.MaxValue) gives an empty page and not an
    // overflow with a negative OFFSET.
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);

    // Adds the errors of "page" and "pageSize" to the errors of the other fields of the request.
    public void Validate(IDictionary<string, string[]> errors)
    {
        if (Page < 1)
        {
            errors["page"] = new[] { "A página tem de ser um número inteiro igual ou superior a 1." };
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errors["pageSize"] = new[] { $"O tamanho da página tem de estar entre 1 e {MaxPageSize}." };
        }
    }
}