namespace LocalHub.Application.Common.Exceptions;

// LocalHub's own error types (API documentation, section 4.6). The API adds the prefix https://localhub.local/erros/.
public static class ErrorTypes
{
    public const string InvalidCredentials = "credenciais-invalidas";
    public const string AccountDeactivated = "conta-desativada";
    public const string MissingProfile = "perfil-em-falta";
    public const string InvalidTransition = "transicao-invalida";
    public const string AdministrationWebOnly = "administracao-so-web";
}