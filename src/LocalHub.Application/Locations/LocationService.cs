using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations.Dtos;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Locations;

public class LocationService(LocalHubDbContext db, LocationResolver resolver, ICurrentUser currentUser)
{
    // E16. The location is resolved before the user is loaded, so when the address is not found or the geocoding
    // service is unavailable the saved location stays as it was.
    public async Task<UserResponse> SetMineAsync(SetLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await resolver.ResolveAsync(request, cancellationToken);

        var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        user.SetLocation(location.Address, location.Coordinates);
        await db.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}