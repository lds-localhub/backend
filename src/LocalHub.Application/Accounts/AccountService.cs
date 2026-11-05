using System.Security.Cryptography;

using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Application.Reviews;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Domain.Services;
using LocalHub.Infrastructure;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LocalHub.Application.Accounts;

public class AccountService
{
    // Administrators are never listed.
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

    // Without characters that are easy to confuse (0/O, 1/l/I): the administrator gives this password to the user.
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
        if (user == null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        return UserResponse.From(user, await _db.FindProfileIdAsync(user.UserId, cancellationToken));
    }

    public async Task<PagedResult<AccountListItemResponse>> ListAsync(
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
        if (roleFilter != null)
        {
            query = query.Where(u => u.Role == roleFilter);
        }

        if (statusFilter != null)
        {
            query = query.Where(u => u.Status == statusFilter);
        }

        int total = await query.CountAsync(cancellationToken);
        var accounts = await query
            .OrderBy(u => u.Name)
            .ThenBy(u => u.UserId) // stable order between pages when two accounts have the same name
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(u => new AccountResponse(u.UserId, u.Name, u.Email, u.Role, u.Status))
            .ToListAsync(cancellationToken);

        var alerts = await GetAlertsAsync(accounts, cancellationToken);
        var items = accounts
            .Select(a => new AccountListItemResponse(
                a.UserId, a.Name, a.Email, a.Role, a.Status, alerts.GetValueOrDefault(a.UserId) ?? []))
            .ToList();

        return new PagedResult<AccountListItemResponse>(page.Page, page.PageSize, total, items);
    }

    // Calculated on each request and never stored.
    private async Task<Dictionary<int, List<AccountAlertResponse>>> GetAlertsAsync(
        IReadOnlyCollection<AccountResponse> accounts, CancellationToken cancellationToken)
    {
        var userIds = accounts.Select(a => a.UserId).ToList();
        var providerIds = accounts.Where(a => a.Role == UserRole.Provider).Select(a => a.UserId).ToList();
        var clientIds = accounts.Where(a => a.Role == UserRole.Client).Select(a => a.UserId).ToList();

        // Services cancelled while ASSIGNED, counted for the user who cancelled them.
        var cancelled = await _db.ServiceStatusHistories.AsNoTracking()
            .Where(h => h.PreviousStatus == ServiceStatus.Assigned
                && h.NewStatus == ServiceStatus.Cancelled
                && userIds.Contains(h.ChangedByUserId))
            .GroupBy(h => h.ChangedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Select(h => h.ServiceId).Distinct().Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var providerAssigned = await _db.Proposals.AsNoTracking()
            .Where(p => p.Status == ProposalStatus.Accepted
                && providerIds.Contains(p.ProfessionalProfile.ServiceProviderId))
            .GroupBy(p => p.ProfessionalProfile.ServiceProviderId)
            .Select(g => new { UserId = g.Key, Count = g.Select(p => p.ServiceId).Distinct().Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var clientAssigned = await _db.Services.AsNoTracking()
            .Where(s => clientIds.Contains(s.ClientId)
                && s.StatusHistory.Any(h => h.NewStatus == ServiceStatus.Assigned))
            .GroupBy(s => s.ClientId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var ratings = await _db.ProfessionalProfiles.AsNoTracking()
            .Where(p => providerIds.Contains(p.ServiceProviderId) && p.AverageRating != null)
            .Select(p => new { UserId = p.ServiceProviderId, p.ProfessionalProfileId, p.AverageRating })
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var reviewCounts = await _db.CountReviewsAsync(
            ratings.Values.Select(r => r.ProfessionalProfileId).ToList(), cancellationToken);

        var result = new Dictionary<int, List<AccountAlertResponse>>();
        foreach (var account in accounts)
        {
            int assigned = account.Role == UserRole.Provider
                ? providerAssigned.GetValueOrDefault(account.UserId)
                : clientAssigned.GetValueOrDefault(account.UserId);
            var alerts = new List<AccountAlertResponse?>
            {
                AccountAlertResponse.ForAssignedCancellations(cancelled.GetValueOrDefault(account.UserId), assigned),
            };
            if (account.Role == UserRole.Provider && ratings.TryGetValue(account.UserId, out var rating))
            {
                alerts.Add(AccountAlertResponse.ForLowAverageRating(
                    rating.AverageRating, reviewCounts.GetValueOrDefault(rating.ProfessionalProfileId)));
            }

            result[account.UserId] = alerts.OfType<AccountAlertResponse>().ToList();
        }

        return result;
    }

    public async Task<AdminSummaryResponse> GetAdminSummaryAsync(CancellationToken cancellationToken)
    {
        var accounts = _db.Users.AsNoTracking().Where(u => u.Role != UserRole.Admin);

        int activeAccounts = await accounts.CountAsync(u => u.Status == ActivationStatus.Active, cancellationToken);
        int inactiveAccounts = await accounts.CountAsync(u => u.Status == ActivationStatus.Inactive, cancellationToken);
        int activeCategories = await _db.ServiceCategories.AsNoTracking()
            .CountAsync(c => c.Status == ActivationStatus.Active, cancellationToken);

        return new AdminSummaryResponse(activeAccounts, inactiveAccounts, activeCategories);
    }

    public async Task<AccountResponse> DeactivateAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null))
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        user.Deactivate(_timeProvider.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(user);
    }

    public async Task<AccountResponse> ActivateAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

        user.Activate();
        await _db.SaveChangesAsync(cancellationToken);

        return AccountResponse.From(user);
    }

    // The account status does not change, so an inactive account can be reset too.
    public async Task<ResetPasswordResponse> ResetPasswordAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null))
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Conta não encontrada.");
        }

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

        // Only returned here: just the hash is stored.
        return new ResetPasswordResponse(temporaryPassword);
    }

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