using System.ComponentModel.DataAnnotations;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Configuration for the JWT issuer and validator. Bound from the
/// <c>Jwt</c> section of <c>appsettings.json</c>. The signing key comes
/// from <c>Jwt:SigningKey</c>; in production, the operator sets the env
/// var <c>Jwt__SigningKey</c> (see <c>secrets.instructions.md</c>).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing key. Must be at least 32 bytes (256 bits).</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token issuer (the API URL). Used in the <c>iss</c> claim and as the JwtBearer <c>ValidIssuer</c>.</summary>
    [Required]
    public string Issuer { get; set; } = "sonrisa-news";

    /// <summary>Audience claim. Frontend uses this to confirm the token is meant for the Sonrisa News UI.</summary>
    [Required]
    public string Audience { get; set; } = "sonrisa-news-web";

    /// <summary>Access-token lifetime. Short — refresh tokens carry the long-lived state.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Refresh-token lifetime. Long; the auth flow rotates the refresh token on every use.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
}
