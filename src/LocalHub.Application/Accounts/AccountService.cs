using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Domain.Accounts;
using LocalHub.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Accounts;

public class AccountService(LocalHubDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    // Filters accepted by E05: the list never includes administrators (RF-F002-01).
    private static readonly Dictionary<string, UserRole> RoleFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CLIENT"] = UserRole.Client,
        ["PROVIDER"] = UserRole.Provider,
    };

    private static readonly Dictionary<string, ActivationStatus> StatusFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACTIVE"] = ActivationStatus.Active,
        ["INACTIVE"] = ActivationStatus.Inactive,
    };

    public async Task<UserResponse> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == currentUser.UserId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        return UserResponse.From(user, await db.FindProfileIdAsync(user.UserId, cancellationToken));
    }

    // E05: accounts of clients and providers, optionally filtered by role and status, ordered by name.
    public async Task<PagedResult<AccountResponse>> ListAsync(
        string? role, string? status, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        UserRole? roleFilter = null;
        if (!string.IsNullOrEmpty(role))
        {
            if (RoleFilters.TryGetValue(role, out var parsedRole))
            {
                roleFilter = parsedRole;
            }
            else
            {
                errors["role"] = ["O papel tem de ser CLIENT (Cliente) ou PROVIDER (Prestador de Serviços)."];
            }
        }

        ActivationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (StatusFilters.TryGetValue(status, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }
            else
            {
                errors["status"] = ["O estado tem de ser ACTIVE (Ativa) ou INACTIVE (Desativada)."];
            }
        }

        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var query = db.Users.AsNoTracking().Where(u => u.Role != UserRole.Admin);
        if (roleFilter is not null)
        {
            query = query.Where(u => u.Role == roleFilter);
        }

        if (statusFilter is not null)
        {
            query = query.Where(u => u.Status == statusFilter);
        }

        int total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.Name)
            .ThenBy(u => u.UserId) // stable order between pages when two accounts have the same name
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(u => new AccountResponse(u.UserId, u.Name, u.Email, u.Role, u.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountResponse>(page.Page, page.PageSize, total, items);
    }

    // E06: the account becomes INACTIVE and its sessions are revoked (RF-F002-02).
    public async Task<AccountResponse> DeactivateAsync(int userId, CancellationToken cancellationToken)
    {
        // Only the sessions that are still open need to be loaded and revoked.
        var user = await db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null))
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        user.Deactivate(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(user);
    }

    // E07: the account becomes ACTIVE and the user can log in again (RF-F002-03).
    public async Task<AccountResponse> ActivateAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        user.Activate();
        await db.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(user);
    }
}