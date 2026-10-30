using LocalHub.Application.Common.Exceptions;
using LocalHub.Domain.Common;
using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Api.Middleware;

// Converts the exceptions thrown by the application and domain layers into Problem Details responses.
public class ErrorHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // If part of the response was already sent, it can no longer be replaced by an error response.
            if (context.Response.HasStarted)
            {
                throw;
            }

            await HandleAsync(context, ex);
        }
    }

    private Task HandleAsync(HttpContext context, Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Dados inválidos",
                    validation.Message,
                    errors: validation.Errors.ToDictionary());

            case InvalidCredentialsException credentials:
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Credenciais inválidas",
                    credentials.Message,
                    type: ErrorTypes.InvalidCredentials);

            case ForbiddenException forbidden:
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Operação não autorizada",
                    forbidden.Message,
                    type: forbidden.ErrorType);

            case CategoryInUseException inUse:
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "Categoria em utilização",
                    inUse.Message);

            case NotFoundException notFound:
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status404NotFound,
                    "Recurso não encontrado",
                    notFound.Message);

            case DomainException domain:
                return HandleDomainAsync(context, domain);

            // The message of the exception explains the technical cause, so it is not sent to the user.
            case GeocodingUnavailableException:
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "Serviço indisponível",
                    GeocodingUnavailableException.UserMessage);

            default:
                _logger.LogError(exception, "Unexpected error while processing the request.");
                return ProblemResponses.WriteAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "Erro interno",
                    "Ocorreu um erro inesperado. Tente novamente mais tarde.");
        }
    }

    private static Task HandleDomainAsync(HttpContext context, DomainException exception)
    {
        Dictionary<string, string[]>? errors = null;
        if (exception.Field is not null)
        {
            errors = new Dictionary<string, string[]>();
            errors[exception.Field] = new[] { exception.Message };
        }

        switch (exception.Kind)
        {
            case DomainErrorKind.Forbidden:
                return ProblemResponses.WriteAsync(
                    context, StatusCodes.Status403Forbidden, "Operação não autorizada", exception.Message);

            case DomainErrorKind.InvalidTransition:
                return ProblemResponses.WriteAsync(
                    context, StatusCodes.Status409Conflict, "Transição inválida", exception.Message,
                    type: ErrorTypes.InvalidTransition);

            default:
                return ProblemResponses.WriteAsync(
                    context, StatusCodes.Status409Conflict, "Conflito", exception.Message, errors: errors,
                    type: exception.ErrorType);
        }
    }
}