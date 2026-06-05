using System.Security.Cryptography;
using System.Text;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// One-way hash for short-lived, single-use tokens (email-verification,
/// password-reset, magic links). SHA-256 is appropriate here because the
/// tokens are high-entropy random values (see <see cref="GenerateOpaqueToken"/>)
/// and have a short lifetime (24h). Argon2id would slow down the legitimate
/// use case for no security gain.
/// </summary>
public interface ITokenHasher
{
    /// <summary>Hashes a token. Returns a lowercase hex string (64 chars for SHA-256).</summary>
    string Hash(string token);

    /// <summary>Generates a cryptographically random opaque token (32 bytes → 64 hex chars or 43 base64url chars).</summary>
    string GenerateOpaqueToken();
}

/// <summary>SHA-256-backed <see cref="ITokenHasher"/>.</summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public string GenerateOpaqueToken()
    {
        // 32 bytes (256 bits) of random data, base64url-encoded. Matches
        // the entropy budget of a SHA-256 hash output.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
