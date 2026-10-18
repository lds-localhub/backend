namespace LocalHub.Application.Locations.Dtos;

// E16: either the address or the pair latitude/longitude, never both.
public record SetLocationRequest(string? Address, decimal? Latitude, decimal? Longitude);