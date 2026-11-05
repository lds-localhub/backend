using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalHub.IntegrationTests.Fixtures;

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