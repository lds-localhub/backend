using LocalHub.Domain.Locations;
using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Geocoding;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Locations;

public static class LocationQueries
{
    public static async Task<Coordinates?> FindLocationAsync(
        this LocalHubDbContext db, int userId, CancellationToken cancellationToken)
    {
        var location = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId && u.Latitude != null && u.Longitude != null)
            .Select(u => new { Latitude = u.Latitude!.Value, Longitude = u.Longitude!.Value })
            .SingleOrDefaultAsync(cancellationToken);

        return location == null ? null : Coordinates.Rounded(location.Latitude, location.Longitude);
    }

    public static decimal? DistanceKm(Coordinates? from, decimal latitude, decimal longitude)
    {
        if (from == null)
        {
            return null;
        }

        return GeoDistance.RoundKm(GeoDistance.HaversineKm(from.Latitude, from.Longitude, latitude, longitude));
    }
}