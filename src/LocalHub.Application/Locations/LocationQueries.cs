using LocalHub.Domain.Locations;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Locations;

public static class LocationQueries
{
    // The registered location of a user, or null while it has none.
    public static async Task<GeoPoint?> FindLocationAsync(
        this LocalHubDbContext db, int userId, CancellationToken cancellationToken)
    {
        var location = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId && u.Latitude != null && u.Longitude != null)
            .Select(u => new { Latitude = u.Latitude!.Value, Longitude = u.Longitude!.Value })
            .SingleOrDefaultAsync(cancellationToken);

        return location is null ? null : GeoPoint.Create(location.Latitude, location.Longitude);
    }

    // The distanceKm of a service seen by a provider (E36 to E38): from the provider's location to the service, with one
    // decimal place, or null while the provider has no location.
    public static decimal? DistanceKm(GeoPoint? from, decimal latitude, decimal longitude) =>
        from is null
            ? null
            : GeoDistance.RoundKm(GeoDistance.HaversineKm(from.Value.Latitude, from.Value.Longitude, latitude, longitude));
}