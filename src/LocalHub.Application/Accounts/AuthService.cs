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

public class AuthService(
    LocalHubDbContext db,
    IPasswordHasher<User> passwordHasher,
    ITokenIssuer tokenIssuer,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    // E01. The fields were validated by the DTO (rule R17); the e-mail already registered depends on the database.
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        string email = request.Email!.Trim();
        if (await EmailExistsAsync(email, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "Já existe uma conta com este e-mail.", "email");
        }

        var user = User.Register(request.Name!, email, request.Role!.Value);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password!));
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_User_email"
        })
        {
            // Two registrations with the same e-mail at the same time: the unique index UX_User_email
            // (LocalHubDbContext) rejects the second one.
            throw new DomainException(DomainErrorKind.Conflict, "Já existe uma conta com este e-mail.", "email");
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

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (user.Status != ActivationStatus.Active)
        {
            throw new ForbiddenException("A sua conta está desativada.", ErrorTypes.AccountDeactivated);
        }

        var session = UserSession.Start(user, timeProvider.GetUtcNow());
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken); // generates the SessionId used in the token

        string token = tokenIssuer.Issue(user, session);
        int? professionalProfileId = await db.FindProfileIdAsync(user.UserId, cancellationToken);
        return new LoginResponse(token, session.ExpiresAt, UserResponse.From(user, professionalProfileId));
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = await db.UserSessions.SingleOrDefaultAsync(
            s => s.SessionId == currentUser.SessionId, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Revoke(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ValidateSessionAsync(int sessionId, CancellationToken cancellationToken)
    {
        var session = await db.UserSessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        if (session is null || !session.IsValid(timeProvider.GetUtcNow()))
        {
            return false;
        }

        return await db.Users.AnyAsync(
            u => u.UserId == session.UserId && u.Status == ActivationStatus.Active, cancellationToken);
    }

    private Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return UsersWithEmail(email).AnyAsync(cancellationToken);
    }

    // E-mails are always stored in lowercase (User.Register), so "Ana@Mail.com" finds "ana@mail.com".
    private IQueryable<User> UsersWithEmail(string email)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return db.Users.Where(u => u.Email == normalized);
    }
}