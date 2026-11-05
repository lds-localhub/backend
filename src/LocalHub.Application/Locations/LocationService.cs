using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Locations.Dtos;
using LocalHub.Application.Profiles;
using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Geocoding;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Locations;

public class LocationService
{
    public const string AddressNotFoundMessage = "Não foi possível obter a morada desta posição. Introduza o endereço.";

    private readonly LocalHubDbContext _db;
    private readonly LocationResolver _resolver;
    private readonly IGeocodingService _geocodingService;
    private readonly ICurrentUser _currentUser;

    public LocationService(
        LocalHubDbContext db, LocationResolver resolver, IGeocodingService geocodingService, ICurrentUser currentUser)
    {
        _db = db;
        _resolver = resolver;
        _geocodingService = geocodingService;
        _currentUser = currentUser;
    }

    public async Task<AddressResponse> ReverseGeocodeAsync(
        decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        string? address = await _geocodingService.ReverseGeocodeAsync(
            Coordinates.Rounded(latitude, longitude), cancellationToken);
        if (address == null)
        {
            throw new ValidationException(AddressNotFoundMessage);
        }

        return new AddressResponse(address);
    }

    // Geocode before changing anything, so a failure keeps the previous location.
    public async Task<UserResponse> SetMineAsync(SetLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await _resolver.ResolveAddressAsync(request.Address!, "address", cancellationToken);

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        user.SetLocation(location.Address, location.Coordinates.Latitude, location.Coordinates.Longitude);
        await _db.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user, await _db.FindProfileIdAsync(user.UserId, cancellationToken));
    }
}