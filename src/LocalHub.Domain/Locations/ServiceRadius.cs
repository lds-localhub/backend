namespace LocalHub.Domain.Locations;


public static class ServiceRadius
{
    public const short MinKm = 1;
    public const short MaxKm = 100;

    private const string Field = "serviceRadiusKm";

  
    public static short Validate(decimal? km)
    {
        if (km is null)
        {
            throw new DomainException(DomainErrorKind.Validation, "Indique o raio de atuação, em quilómetros.", Field);
        }

        if (km.Value != decimal.Truncate(km.Value))
        {
            throw new DomainException(DomainErrorKind.Validation, "O raio de atuação tem de ser um número inteiro de quilómetros.", Field);
        }

        if (km.Value is < MinKm or > MaxKm)
        {
            throw new DomainException(DomainErrorKind.Validation, "O raio de atuação tem de estar entre 1 e 100 km.", Field);
        }

        return (short)km.Value;
    }
}