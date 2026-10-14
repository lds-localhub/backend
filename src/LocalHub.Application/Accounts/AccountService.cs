using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Accounts;

public class AccountService(LocalHubDbContext db, ICurrentUser currentUser)
{
    public async Task<UserResponse> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == currentUser.UserId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        return UserResponse.From(user);
    }
}