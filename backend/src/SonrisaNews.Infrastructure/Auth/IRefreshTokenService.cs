using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Manages the refresh-token lifecycle. Tokens are opaque random strings;
/// only the SHA-256 hash is stored in the DB. The plaintext lives in the
/// httpOnly cookie set by the auth flow. Every successful refresh
/// <em>rotates</em> the token (issues a new one and revokes the old) to
/// limit the blast radius of a stolen cookie.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token for the user. The plaintext is returned; only the hash is stored.</summary>
    Task<(RefreshToken Token, string Plaintext)> IssueAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Validates a presented refresh-token plaintext, rotates it (revokes
    /// the old, issues a new), and returns the new plaintext + the user.
    /// Returns <c>null</c> if the token is unknown, expired, or revoked.
    /// </summary>
    Task<(RefreshToken NewToken, string NewPlaintext, User User)?> RotateAsync(
        string presentedToken, CancellationToken ct = default);

    /// <summary>Revokes a specific refresh token. Idempotent.</summary>
    Task RevokeAsync(string presentedToken, CancellationToken ct = default);
}

public sealed class RefreshTokenService(
    IDbContextFactory<SonrisaNewsDbContext> dbContextFactory,
    ITokenHasher tokenHasher,
    IOptions<JwtOptions> options,
    TimeProvider clock) : IRefreshTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<(RefreshToken Token, string Plaintext)> IssueAsync(User user, CancellationToken ct = default)
    {
        var plaintext = tokenHasher.GenerateOpaqueToken();
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHasher.Hash(plaintext),
            ExpiresAt = clock.GetUtcNow() + _options.RefreshTokenLifetime,
            CreatedAt = clock.GetUtcNow(),
        };

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return (token, plaintext);
    }

    public async Task<(RefreshToken NewToken, string NewPlaintext, User User)?> RotateAsync(
        string presentedToken, CancellationToken ct = default)
    {
        var presentedHash = tokenHasher.Hash(presentedToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var existing = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, ct);
        if (existing is null) return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null) return null;
        if (existing.RevokedAt is not null) return null;
        if (existing.ExpiresAt < clock.GetUtcNow()) return null;
        if (user.Status is not (UserStatus.Active or UserStatus.PendingEmailVerification))
        {
            // Soft-deleted, suspended, or otherwise unavailable accounts
            // cannot refresh — the JWT middleware would 401 them on the
            // next request, but revoking the refresh here means the
            // client gets a clean "refresh failed" rather than a stuck
            // 401 on the next call.
            return null;
        }

        // Revoke the old token and issue the new one in a single transaction.
        // A crash between the revoke and the insert would leave the user
        // logged out, but the httpOnly cookie is the only way the client
        // can present the old token again, so a re-issue of the cookie
        // fixes the problem.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        existing.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var newPlaintext = tokenHasher.GenerateOpaqueToken();
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = tokenHasher.Hash(newPlaintext),
            ExpiresAt = clock.GetUtcNow() + _options.RefreshTokenLifetime,
            CreatedAt = clock.GetUtcNow(),
            ReplacedById = null, // populated after the insert when we have newToken.Id
        };

        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);

        existing.ReplacedById = newToken.Id;
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return (newToken, newPlaintext, user);
    }

    public async Task RevokeAsync(string presentedToken, CancellationToken ct = default)
    {
        var presentedHash = tokenHasher.Hash(presentedToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == presentedHash, ct);
        if (existing is null || existing.RevokedAt is not null) return;
        existing.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }
}
