using LocalHub.Infrastructure.Geocoding;

namespace LocalHub.Application.Locations;

public record ResolvedLocation(string Address, Coordinates Coordinates);