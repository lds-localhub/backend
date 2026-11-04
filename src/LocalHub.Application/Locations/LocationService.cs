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

    // E35: the address of the position of the device, used by the mobile application to fill the address field.
    // Nothing is saved: the address is only used when the user confirms it and submits the form (RF-F015-06, rule R3).
    // The ranges of the coordinates were validated by the controller (RF-F015-04, rule R17).
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

    // E16 (RF-F015-02, RF-F015-03): the location of the client, received as an address. The address is converted before
    // the user is loaded, so when it is not found or the geocoding service is unavailable the saved location stays as
    // it was (rule R2).
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