namespace LocalHub.Application.Common.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("O e-mail ou a palavra-passe estão incorretos.")
    {
    }
}