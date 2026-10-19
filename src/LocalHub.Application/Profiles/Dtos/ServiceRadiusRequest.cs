namespace LocalHub.Application.Profiles.Dtos;

// Body of E17. A decimal, so that 25.5 is rejected with the message of ServiceRadius.
public record ServiceRadiusRequest(decimal? ServiceRadiusKm);