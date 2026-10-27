using LocalHub.Application.Accounts.Dtos;
using LocalHub.Application.Common;
using LocalHub.Application.Common.Exceptions;
using LocalHub.Application.Profiles;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;
using LocalHub.Infrastructure;
using LocalHub.Infrastructure.Security;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LocalHub.Application.Accounts;

public class AuthService
{
    private readonly LocalHubDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        LocalHubDbContext db,
        IPasswordHasher<User> passwordHasher,
        ITokenIssuer tokenIssuer,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // E01. The fields were validated by the DTO (rule R17); the e-mail already registered depends on the database.
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        string email = request.Email!.Trim();
        if (await EmailExistsAsync(email, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "Já existe uma conta com este e-mail.", "email");
        }

        var user = User.Register(request.Name!, email, request.Role!.Value);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, request.Password!));
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two registrations with the same e-mail at the same time: the unique index UX_User_email
            // (UserConfiguration) rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && postgres.ConstraintName == "UX_User_email")
            {
                throw new DomainException(DomainErrorKind.Conflict, "Já existe uma conta com este e-mail.", "email");
            }

            // Any other error of the database is not expected: the request gets 500.
            throw;
        }

        return new RegisterResponse(user.UserId, user.Name, user.Email, user.Role, user.Status);
    }

    // E02. The fields were validated by the DTO (rule R17).
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await UsersWithEmail(request.Email!).SingleOrDefaultAsync(cancellationToken);

        // Unknown e-mail and wrong password get the same response (RF-F001-02).
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (user.Status != ActivationStatus.Active)
        {
            throw new ForbiddenException("A sua conta está desativada.", ErrorTypes.AccountDeactivated);
        }

        var session = UserSession.Start(user, _timeProvider.GetUtcNow());
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken); // generates the SessionId used in the token

        string token = _tokenIssuer.Issue(user, session);
        int? professionalProfileId = await _db.FindProfileIdAsync(user.UserId, cancellationToken);
        return new LoginResponse(token, session.ExpiresAt, UserResponse.From(user, professionalProfileId));
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions.SingleOrDefaultAsync(
            s => s.SessionId == _currentUser.SessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.Revoke(_timeProvider.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);
    }

    // E40. The fields were validated by the DTO (rule R17); the current password depends on the database.
    // The other sessions of the user are revoked and the current one stays valid (RF-F001-07, rule R18).
    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        int currentSessionId = _currentUser.SessionId;
        var user = await _db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null && s.SessionId != currentSessionId))
            .SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("Utilizador não encontrado.");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword!);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new ValidationException("currentPassword", "A palavra-passe atual está incorreta.");
        }

        user.SetPasswordHash(_passwordHasher.HashPassword(user, request.NewPassword!));
        var now = _timeProvider.GetUtcNow();
        foreach (var session in user.Sessions)
        {
            session.Revoke(now);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ValidateSessionAsync(int sessionId, CancellationToken cancellationToken)
    {
        // A single query: the session with its user, to check the account state too.
        var session = await _db.UserSessions.AsNoTracking()
            .Include(s => s.User)
            .SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        return session is not null
               && session.IsValid(_timeProvider.GetUtcNow())
               && session.User.Status == ActivationStatus.Active;
    }

    private Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return UsersWithEmail(email).AnyAsync(cancellationToken);
    }

    // E-mails are always stored in lowercase (User.Register), so "Ana@Mail.com" finds "ana@mail.com".
    private IQueryable<User> UsersWithEmail(string email)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return _db.Users.Where(u => u.Email == normalized);
    }
}