using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Issues and validates JWT access tokens. The refresh-token lifecycle
/// (rotation, revocation) lives in <see cref="IRefreshTokenService"/>; this
/// service is the JWT-only half.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>Issues a signed JWT for the given user. The token carries <c>sub</c> (user id), <c>email</c>, and <c>role</c> claims.</summary>
    (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(User user);

    /// <summary>Extracts the user id from a JWT's <c>sub</c> claim. Returns null on parse failure.</summary>
    Guid? GetUserIdFromToken(string token);
}

/// <summary>
/// HS256-signed JWT issuer. Reads <see cref="JwtOptions"/> from
/// configuration and signs with the configured key. The same key
/// validates tokens on the JwtBearer middleware side.
/// </summary>
public sealed class JwtAuthTokenService(
    IOptions<JwtOptions> options,
    ILogger<JwtAuthTokenService> logger) : IAuthTokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(User user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now + _options.AccessTokenLifetime;

        // The role is NOT in the JWT (user rule, 2026-06-05 — the role lives
        // in the DB and is resolved per-request by RbacPolicyHandler via the
        // UserRoles ⨝ RolePermissions join). Stale roles in the JWT would
        // outlast a role change in the DB, so we never carry one here.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.WriteToken(token);
        logger.LogInformation("Issued access token for user {UserId} until {ExpiresAt}", user.Id, expires);
        return (jwt, expires);
    }

    public Guid? GetUserIdFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read user id from token");
            return null;
        }
    }
}
