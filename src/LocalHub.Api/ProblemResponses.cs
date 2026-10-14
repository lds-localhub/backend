using Microsoft.AspNetCore.Mvc;

namespace LocalHub.Api;

// Writes error responses in the Problem Details format (RFC 9457) described in the API documentation (section 4.6).
public static class ProblemResponses
{
    public static Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;

        if (errors is null)
        {
            var problem = new ProblemDetails { Title = title, Status = status, Detail = detail };
            return context.Response.WriteAsJsonAsync(problem, options: null, "application/problem+json");
        }

        // HttpValidationProblemDetails also has the "errors" field, with the message of each field.
        var validationProblem = new HttpValidationProblemDetails(errors) { Title = title, Status = status, Detail = detail };
        return context.Response.WriteAsJsonAsync(validationProblem, options: null, "application/problem+json");
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