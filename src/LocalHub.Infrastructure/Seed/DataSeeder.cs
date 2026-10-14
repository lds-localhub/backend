using LocalHub.Domain.Accounts;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LocalHub.Infrastructure.Seed;

public class DataSeeder(LocalHubDbContext db, IPasswordHasher<User> passwordHasher, IOptions<SeedOptions> options)
{
    private readonly string _password = options.Value.DefaultPassword;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await AddIfMissingAsync(User.CreateAdministrator("Ana Almeida", "ana.almeida@localhub.test"),
            cancellationToken);
        await AddIfMissingAsync(User.CreateAdministrator("Rui Rocha", "rui.rocha@localhub.test"), cancellationToken);
        await AddIfMissingAsync(User.Register("Carla Costa", "carla.costa@localhub.test", UserRole.Client),
            cancellationToken);
        await AddIfMissingAsync(User.Register("Hugo Henriques", "hugo.henriques@localhub.test", UserRole.Provider),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddIfMissingAsync(User user, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == user.Email, cancellationToken))
        {
            return;
        }

        user.SetPasswordHash(passwordHasher.HashPassword(user, _password));
        db.Users.Add(user);
    }
}