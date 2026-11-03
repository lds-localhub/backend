using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LocalHub.Infrastructure.Seed;

public class DataSeeder
{
    private readonly string _password;

    private readonly LocalHubDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DataSeeder(LocalHubDbContext db, IPasswordHasher<User> passwordHasher, IOptions<SeedOptions> options)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _password = options.Value.DefaultPassword;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Func<User, string, string> hashPassword = _passwordHasher.HashPassword;
        await AddIfMissingAsync(
            User.CreateAdministrator("Ana Almeida", "ana.almeida@localhub.test", _password, hashPassword),
            cancellationToken);
        await AddIfMissingAsync(
            User.CreateAdministrator("Rui Rocha", "rui.rocha@localhub.test", _password, hashPassword),
            cancellationToken);
        await AddIfMissingAsync(
            User.Register("Carla Costa", "carla.costa@localhub.test", UserRole.Client, _password, hashPassword),
            cancellationToken);
        await AddIfMissingAsync(
            User.Register("Hugo Henriques", "hugo.henriques@localhub.test", UserRole.Provider, _password,
                hashPassword),
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddIfMissingAsync(User user, CancellationToken cancellationToken)
    {
        if (await _db.Users.AnyAsync(u => u.Email == user.Email, cancellationToken))
        {
            return;
        }

        _db.Users.Add(user);
    }
}