using LocalHub.Domain.Locations;

namespace LocalHub.Application.Locations;

public record ResolvedLocation(string Address, GeoPoint Coordinates);