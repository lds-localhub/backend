namespace LocalHub.Application.Common;

public record PageRequest(int Page = PageRequest.DefaultPage, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    // long avoids an overflow with a very large page.
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);

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