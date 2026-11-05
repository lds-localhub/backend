using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api;

public static class ProblemResponses
{
    private const string TypePrefix = "https://localhub.local/erros/";

    private static string DefaultType(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status503ServiceUnavailable => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    };

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        IDictionary<string, string[]>? errors = null,
        string? type = null)
    {
        context.Response.StatusCode = status;

        ProblemDetails problem = errors == null ? new ProblemDetails() : new HttpValidationProblemDetails(errors);
        Fill(problem, context, status, title, detail, type);
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, "application/problem+json");
    }

    public static void Fill(ProblemDetails problem, HttpContext context, int status, string title, string detail, string? type)
    {
        problem.Type = type == null ? DefaultType(status) : TypePrefix + type;
        problem.Title = title;
        problem.Status = status;
        problem.Detail = detail;
        problem.Instance = context.Request.Path;
    }

    public static Task WriteSessionInvalidAsync(HttpContext context)
    {
        return WriteAsync(
            context,
            StatusCodes.Status401Unauthorized,
            "Sessão inválida",
            "A sua sessão terminou. Inicie sessão novamente.");
    }

    public static Task WriteForbiddenAsync(HttpContext context)
    {
        return WriteAsync(
            context,
            StatusCodes.Status403Forbidden,
            "Operação não autorizada",
            "Não tem permissão para realizar esta operação.");
    }
}