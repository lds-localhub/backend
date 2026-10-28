using System.Security.Cryptography;

using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Infrastructure;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Accounts;

public class AccountService
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

    // Characters of the temporary password (E41). Characters that are easy to confuse (0/O, 1/l/I) are left out,
    // because the administrator gives the password to the user outside the system.
    private const string PasswordLetters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string PasswordDigits = "23456789";
    private const int TemporaryPasswordLength = 10;

    private readonly LocalHubDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AccountService(
        LocalHubDbContext db,
        IPasswordHasher<User> passwordHasher,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<UserResponse> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        return UserResponse.From(user, await _db.FindProfileIdAsync(user.UserId, cancellationToken));
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
                errors["role"] = new[] { "O papel tem de ser CLIENT (Cliente) ou PROVIDER (Prestador de Serviços)." };
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
                errors["status"] = new[] { "O estado tem de ser ACTIVE (Ativa) ou INACTIVE (Desativada)." };
            }
        }

        page.Validate(errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var query = _db.Users.AsNoTracking().Where(u => u.Role != UserRole.Admin);
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
        var user = await _db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null))
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        user.Deactivate(_timeProvider.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(user);
    }

    // E07: the account becomes ACTIVE and the user can log in again (RF-F002-03).
    public async Task<AccountResponse> ActivateAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        user.Activate();
        await _db.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(user);
    }

    // E41: a new temporary password for a client or provider account, and all its sessions are revoked
    // (RF-F002-05, rule R18). The state of the account does not change, so an inactive account can be reset too.
    public async Task<ResetPasswordResponse> ResetPasswordAsync(int userId, CancellationToken cancellationToken)
    {
        // Only the sessions that are still open need to be loaded and revoked.
        var user = await _db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null))
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        // Same rule as the deactivation (RF-F002-02, rule 1).
        if (user.Role == UserRole.Admin)
        {
            throw new DomainException(
                DomainErrorKind.Conflict, "A palavra-passe das contas de Administrador não pode ser redefinida.");
        }

        string temporaryPassword = GenerateTemporaryPassword();
        user.SetPasswordHash(_passwordHasher.HashPassword(user, temporaryPassword));
        var now = _timeProvider.GetUtcNow();
        foreach (var session in user.Sessions)
        {
            session.Revoke(now);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Returned only here: only the hash is stored (RNF-01).
        return new ResetPasswordResponse(temporaryPassword);
    }

    // Random password with the rules of the registration (RF-F001-01, rule 3): at least one letter and one digit.
    private static string GenerateTemporaryPassword()
    {
        string password;
        do
        {
            password = RandomNumberGenerator.GetString(PasswordLetters + PasswordDigits, TemporaryPasswordLength);
        }
        while (!password.Any(char.IsLetter) || !password.Any(char.IsDigit));

        return password;
    }
}