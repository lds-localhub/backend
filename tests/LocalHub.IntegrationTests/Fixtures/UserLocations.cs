using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalHub.IntegrationTests.Fixtures;

// The coordinates saved in User, which E02, E04 and E16 do not return (API documentation, E04): they are read from the
// database.
public static class UserLocations
{
    public static async Task<(decimal? Latitude, decimal? Longitude)> ReadUserCoordinatesAsync(
        LocalHubApiFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalHubDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        return (user.Latitude, user.Longitude);
    }
}