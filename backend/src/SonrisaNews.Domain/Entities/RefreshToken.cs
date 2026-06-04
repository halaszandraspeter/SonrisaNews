namespace SonrisaNews.Domain.Entities;

/// <summary>
/// Opaque refresh token issued at sign-in, stored hashed. The auth flow
/// rotates refresh tokens on every use (<c>ReplacedById</c>); revoking a
/// refresh sets <see cref="RevokedAt"/>.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hashed token value. The plaintext only ever lives in the httpOnly cookie.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The id of the refresh token that replaced this one on rotation. Null if this token was never rotated.</summary>
    public Guid? ReplacedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
