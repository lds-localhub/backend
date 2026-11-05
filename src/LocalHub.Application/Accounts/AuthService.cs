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
using Microsoft.Extensions.Options;

using Npgsql;

namespace LocalHub.Application.Accounts;

public class AuthService
{
    private readonly LocalHubDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly SupportOptions _support;

    public AuthService(
        LocalHubDbContext db,
        IPasswordHasher<User> passwordHasher,
        ITokenIssuer tokenIssuer,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IOptions<SupportOptions> support)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _support = support.Value;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        string email = request.Email!.Trim();
        if (await EmailExistsAsync(email, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "Já existe uma conta com este e-mail.", "email");
        }

        var user = User.Register(request.Name!, email, request.Role!.Value, request.Password!,
            _passwordHasher.HashPassword);
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two registrations at the same time: the unique index rejects the second one.
            if (ex.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && postgres.ConstraintName == "UX_User_email")
            {
                throw new DomainException(DomainErrorKind.Conflict, "Já existe uma conta com este e-mail.", "email");
            }

            throw;
        }

        return new RegisterResponse(user.UserId, user.Name, user.Email, user.Role, user.Status);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await UsersWithEmail(request.Email!).SingleOrDefaultAsync(cancellationToken);

        // Same response for an unknown e-mail and a wrong password.
        if (user == null)
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
            throw new ForbiddenException(
                $"A sua conta foi desativada. Para saber o motivo, contacte {_support.Email}.",
                ErrorTypes.AccountDeactivated);
        }

        if (user.Role == UserRole.Admin && request.Client == LoginClient.Mobile)
        {
            throw new ForbiddenException(
                "A administração está disponível apenas na aplicação web.", ErrorTypes.AdministrationWebOnly);
        }

        var session = UserSession.Start(user, _timeProvider.GetUtcNow());
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken); // generates the SessionId used in the token

        string token = _tokenIssuer.Issue(user, session);
        int? professionalProfileId = await _db.FindProfileIdAsync(user.UserId, cancellationToken);
        return new LoginResponse(token, UserResponse.From(user, professionalProfileId));
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions.SingleOrDefaultAsync(
            s => s.SessionId == _currentUser.SessionId, cancellationToken);
        if (session == null)
        {
            return;
        }

        session.Revoke(_timeProvider.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        int currentSessionId = _currentUser.SessionId;
        var user = await _db.Users
            .Include(u => u.Sessions.Where(s => s.RevokedAt == null && s.SessionId != currentSessionId))
            .SingleOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
        if (user == null)
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
        var session = await _db.UserSessions.AsNoTracking()
            .Include(s => s.User)
            .SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        return session != null
               && session.IsValid(_timeProvider.GetUtcNow())
               && session.User.Status == ActivationStatus.Active;
    }

    private Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return UsersWithEmail(email).AnyAsync(cancellationToken);
    }

    // E-mails are stored in lowercase.
    private IQueryable<User> UsersWithEmail(string email)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return _db.Users.Where(u => u.Email == normalized);
    }
}