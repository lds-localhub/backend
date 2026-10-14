using LocalHub.Domain.Common;

namespace LocalHub.Domain.Locations;

/// <summary>
/// Validates the service radius of a provider: a whole number of kilometres between 1 and 100.
/// </summary>
public static class ServiceRadius
{
    public const short MinKm = 1;
    public const short MaxKm = 100;

    private const string Field = "serviceRadiusKm";

    // Receives a decimal so that 25.5 is rejected with a clear message instead of failing the JSON reading.
    public static short Validate(decimal? km)
    {
        if (km is null)
        {
            throw new DomainException(DomainErrorKind.Validation, "Indique o raio de atuação, em quilómetros.", Field);
        }

        if (km.Value != decimal.Truncate(km.Value))
        {
            throw new DomainException(DomainErrorKind.Validation,
                "O raio de atuação tem de ser um número inteiro de quilómetros.", Field);
        }

        if (km.Value is < MinKm or > MaxKm)
        {
            throw new DomainException(DomainErrorKind.Validation, "O raio de atuação tem de estar entre 1 e 100 km.", Field);
        }

        return (short)km.Value;
    }
}