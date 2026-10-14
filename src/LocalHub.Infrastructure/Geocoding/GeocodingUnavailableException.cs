namespace LocalHub.Infrastructure.Geocoding;


public sealed class GeocodingUnavailableException : Exception
{
    public const string UserMessage =
        "O serviço de localização não está disponível. Tente novamente dentro de alguns minutos.";

    public GeocodingUnavailableException()
        : base(UserMessage)
    {
    }

    public GeocodingUnavailableException(string message)
        : base(message)
    {
    }

    public GeocodingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}