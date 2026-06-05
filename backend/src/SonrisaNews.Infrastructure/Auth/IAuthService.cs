using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>Outcomes of an authentication request. The auth flow's controller translates these to HTTP status codes.</summary>
public enum AuthOutcome
{
    Success,
    InvalidInput,
    EmailAlreadyExists,
    InvalidCredentials,
    EmailNotVerified,
    InvalidToken,
    TokenExpired,
    UserNotFound,
    AccountSuspended,
}

/// <summary>Result envelope for an auth-flow operation. <see cref="Outcome"/> is the typed error; <see cref="User"/> is the loaded user on success.</summary>
/// <typeparam name="TPayload">The success payload (e.g. <c>(AccessToken, ExpiresAt, RefreshTokenPlaintext)</c>).</typeparam>
public sealed record AuthResult<TPayload>(
    AuthOutcome Outcome,
    TPayload? Payload = default,
    User? User = null,
    string? ErrorDetail = null)
{
    public bool IsSuccess => Outcome == AuthOutcome.Success;

    public static AuthResult<TPayload> Success(TPayload payload, User user) =>
        new(AuthOutcome.Success, Payload: payload, User: user);

    public static AuthResult<TPayload> Failure(AuthOutcome outcome, string detail) =>
        new(outcome, ErrorDetail: detail);
}

/// <summary>
/// The MVP auth flow. Implements signup, signin, refresh, verify-email,
/// forgot-password, reset-password, and signout. The service is the
/// authoritative business-logic seam; the controller is a thin HTTP
/// adapter.
/// </summary>
/// <remarks>
/// The service uses <c>IDbContextFactory&lt;SonrisaNewsDbContext&gt;</c>
/// rather than a scoped <c>DbContext</c> because the seeded admin user
/// (wave 3) and the sign-in flow both need to write to the DB and the
/// factory pattern avoids accidentally sharing a tracked entity across
/// the bootstrap hosted service and the API request pipeline.
/// </remarks>
public interface IAuthService
{
    Task<AuthResult<AuthTokens>> SignUpAsync(SignUpRequest request, CancellationToken ct = default);

    Task<AuthResult<AuthTokens>> SignInAsync(SignInRequest request, CancellationToken ct = default);

    Task<AuthResult<AuthTokens>> RefreshAsync(string presentedRefreshToken, CancellationToken ct = default);

    Task<AuthOutcome> VerifyEmailAsync(string presentedToken, CancellationToken ct = default);

    Task<AuthOutcome> RequestPasswordResetAsync(string email, CancellationToken ct = default);

    Task<AuthOutcome> ResetPasswordAsync(string presentedToken, string newPassword, CancellationToken ct = default);

    Task SignOutAsync(string presentedRefreshToken, CancellationToken ct = default);
}

/// <summary>Concrete auth flow.</summary>
public sealed class AuthService(
    IDbContextFactory<SonrisaNewsDbContext> dbContextFactory,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    IAuthTokenService tokenService,
    IRefreshTokenService refreshTokens,
    IEmailSender emailSender,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>Verification and password-reset tokens expire after this long.</summary>
    private static readonly TimeSpan OneTimeTokenLifetime = TimeSpan.FromHours(24);

    public async Task<AuthResult<AuthTokens>> SignUpAsync(SignUpRequest request, CancellationToken ct = default)
    {
        if (!IsValidEmail(request.Email)) return AuthResult<AuthTokens>.Failure(AuthOutcome.InvalidInput, "Email is malformed.");
        if (!IsValidPassword(request.Password)) return AuthResult<AuthTokens>.Failure(AuthOutcome.InvalidInput, "Password must be at least 8 characters and contain a digit and a letter.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var normalized = request.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.AnyAsync(u => u.Email == normalized, ct);
        if (existing) return AuthResult<AuthTokens>.Failure(AuthOutcome.EmailAlreadyExists, "An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            PasswordHash = passwordHasher.Hash(request.Password),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? normalized : request.DisplayName.Trim(),
            TimeZone = request.TimeZone ?? "UTC",
            Status = UserStatus.PendingEmailVerification,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Users.Add(user);

        // Every new signup gets the `User` role by default. The role id
        // comes from the seeded Roles table (the migration inserts the
        // three baseline roles with deterministic Guids; see
        // RolesCatalogSeed in the AddRbacCatalog migration).
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = RolesCatalogSeed.UserRoleId,
            CreatedAt = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);

        // Issue a one-time email-verification token. The token is hashed
        // before insert; the plaintext is in the email body.
        var verificationPlaintext = tokenHasher.GenerateOpaqueToken();
        db.EmailVerifications.Add(new EmailVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = tokenHasher.Hash(verificationPlaintext),
            ExpiresAt = clock.GetUtcNow() + OneTimeTokenLifetime,
            CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        await emailSender.SendAsync(
            user.Email,
            "Verify your Sonrisa News email",
            $"Welcome to Sonrisa News. Confirm your email by opening this link:\n\n/verify?token={verificationPlaintext}",
            ct);

        // The user can sign in immediately and explore the read-only
        // dashboard; gated routes return 403 EmailNotVerified until
        // verification. This is the Q1 default ("verify before
        // exploring" was the alternative; both are valid).
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult<AuthTokens>> SignInAsync(SignInRequest request, CancellationToken ct = default)
    {
        if (!IsValidEmail(request.Email)) return AuthResult<AuthTokens>.Failure(AuthOutcome.InvalidCredentials, "Email or password is wrong.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null) return AuthResult<AuthTokens>.Failure(AuthOutcome.InvalidCredentials, "Email or password is wrong.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Sign-in failed for {Email}: password mismatch", normalized);
            return AuthResult<AuthTokens>.Failure(AuthOutcome.InvalidCredentials, "Email or password is wrong.");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return AuthResult<AuthTokens>.Failure(AuthOutcome.AccountSuspended, "Account is suspended.");
        }
        if (user.Status == UserStatus.SoftDeleted)
        {
            return AuthResult<AuthTokens>.Failure(AuthOutcome.AccountSuspended, "Account is scheduled for deletion.");
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult<AuthTokens>> RefreshAsync(string presentedRefreshToken, CancellationToken ct = default)
    {
        var result = await refreshTokens.RotateAsync(presentedRefreshToken, ct);
        if (result is null) return AuthResult<AuthTokens>.Failure(AuthOutcome.InvalidToken, "Refresh token is unknown, expired, or revoked.");

        var (newToken, newPlaintext, user) = result.Value;
        var (accessToken, accessExpires) = tokenService.IssueAccessToken(user);
        return AuthResult<AuthTokens>.Success(
            new AuthTokens(accessToken, accessExpires, newPlaintext, newToken.ExpiresAt),
            user);
    }

    public async Task<AuthOutcome> VerifyEmailAsync(string presentedToken, CancellationToken ct = default)
    {
        var hash = tokenHasher.Hash(presentedToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var record = await db.EmailVerifications.FirstOrDefaultAsync(v => v.Token == hash, ct);
        if (record is null) return AuthOutcome.InvalidToken;
        if (record.UsedAt is not null) return AuthOutcome.InvalidToken;
        if (record.ExpiresAt < clock.GetUtcNow()) return AuthOutcome.TokenExpired;

        record.UsedAt = clock.GetUtcNow();
        var user = await db.Users.FirstAsync(u => u.Id == record.UserId, ct);
        if (user.Status == UserStatus.PendingEmailVerification)
        {
            user.Status = UserStatus.Active;
        }
        await db.SaveChangesAsync(ct);
        return AuthOutcome.Success;
    }

    public async Task<AuthOutcome> RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        // Per security best practice, return Success even when the email
        // doesn't match a user — otherwise this is a user-enumeration
        // endpoint. The fake success path silently logs and returns; the
        // real path sends the email.
        if (!IsValidEmail(email)) return AuthOutcome.Success;
        var normalized = email.Trim().ToLowerInvariant();

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null)
        {
            logger.LogInformation("Password-reset request for unknown email {Email}; ignoring", normalized);
            return AuthOutcome.Success;
        }

        var plaintext = tokenHasher.GenerateOpaqueToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = tokenHasher.Hash(plaintext),
            ExpiresAt = clock.GetUtcNow() + OneTimeTokenLifetime,
            CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        await emailSender.SendAsync(
            user.Email,
            "Reset your Sonrisa News password",
            $"We received a password-reset request. If this was you, open this link to set a new password:\n\n/reset?token={plaintext}\n\nThe link expires in 24 hours.",
            ct);
        return AuthOutcome.Success;
    }

    public async Task<AuthOutcome> ResetPasswordAsync(string presentedToken, string newPassword, CancellationToken ct = default)
    {
        if (!IsValidPassword(newPassword)) return AuthOutcome.InvalidInput;

        var hash = tokenHasher.Hash(presentedToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var record = await db.PasswordResetTokens.FirstOrDefaultAsync(v => v.Token == hash, ct);
        if (record is null) return AuthOutcome.InvalidToken;
        if (record.UsedAt is not null) return AuthOutcome.InvalidToken;
        if (record.ExpiresAt < clock.GetUtcNow()) return AuthOutcome.TokenExpired;

        var user = await db.Users.FirstAsync(u => u.Id == record.UserId, ct);
        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.UpdatedAt = clock.GetUtcNow();
        record.UsedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        // Best-effort: revoke all of the user's outstanding refresh tokens
        // so a stolen session cookie is invalidated at the same time the
        // password changes.
        var outstandingTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in outstandingTokens) t.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return AuthOutcome.Success;
    }

    public Task SignOutAsync(string presentedRefreshToken, CancellationToken ct = default) =>
        refreshTokens.RevokeAsync(presentedRefreshToken, ct);

    private async Task<AuthResult<AuthTokens>> IssueTokensAsync(User user, CancellationToken ct = default)
    {
        var (accessToken, accessExpires) = tokenService.IssueAccessToken(user);
        var (_, refreshPlaintext) = await refreshTokens.IssueAsync(user, ct);
        // Re-fetch the just-issued refresh-token row to surface its
        // actual ExpiresAt. The rotate path already returns this; here
        // we approximate with the configured lifetime.
        var refreshExpires = clock.GetUtcNow() + TimeSpan.FromDays(30);
        return AuthResult<AuthTokens>.Success(
            new AuthTokens(accessToken, accessExpires, refreshPlaintext, refreshExpires),
            user);
    }

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email);

    private static bool IsValidPassword(string password) =>
        !string.IsNullOrEmpty(password) && password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit);
}

/// <summary>Input shape for <see cref="IAuthService.SignUpAsync"/>.</summary>
public sealed record SignUpRequest(string Email, string Password, string? DisplayName, string? TimeZone);

/// <summary>Input shape for <see cref="IAuthService.SignInAsync"/>.</summary>
public sealed record SignInRequest(string Email, string Password);

/// <summary>Successful auth-flow output. The refresh-token plaintext goes into the httpOnly cookie; the access token into the response body.</summary>
public sealed record AuthTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
