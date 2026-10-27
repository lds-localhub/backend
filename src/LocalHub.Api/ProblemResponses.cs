using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api;

// Writes error responses in the Problem Details format (RFC 9457) described in the API documentation (section 4.6).
public static class ProblemResponses
{
    private const string TypePrefix = "https://localhub.local/erros/";

    // The same links that ASP.NET Core uses by default.
    private static string DefaultType(int status)
    {
        switch (status)
        {
            case StatusCodes.Status400BadRequest:
                return "https://tools.ietf.org/html/rfc9110#section-15.5.1";
            case StatusCodes.Status401Unauthorized:
                return "https://tools.ietf.org/html/rfc9110#section-15.5.2";
            case StatusCodes.Status403Forbidden:
                return "https://tools.ietf.org/html/rfc9110#section-15.5.4";
            case StatusCodes.Status404NotFound:
                return "https://tools.ietf.org/html/rfc9110#section-15.5.5";
            case StatusCodes.Status409Conflict:
                return "https://tools.ietf.org/html/rfc9110#section-15.5.10";
            case StatusCodes.Status503ServiceUnavailable:
                return "https://tools.ietf.org/html/rfc9110#section-15.6.4";
            default:
                return "https://tools.ietf.org/html/rfc9110#section-15.6.1";
        }
    }

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        IDictionary<string, string[]>? errors = null,
        string? type = null)
    {
        context.Response.StatusCode = status;

        // HttpValidationProblemDetails also has the "errors" field, with the message of each field.
        ProblemDetails problem = errors is null ? new ProblemDetails() : new HttpValidationProblemDetails(errors);
        Fill(problem, context, status, title, detail, type);
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, "application/problem+json");
    }

    // type: one of ErrorTypes, or null for the default type of the status code (link to the section of RFC 9110).
    public static void Fill(ProblemDetails problem, HttpContext context, int status, string title, string detail, string? type)
    {
        problem.Type = type is null ? DefaultType(status) : TypePrefix + type;
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