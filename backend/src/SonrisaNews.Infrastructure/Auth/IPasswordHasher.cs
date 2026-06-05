namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Hashes and verifies passwords. The default implementation uses BCrypt
/// with a work factor of 12 (the same value the <c>seed-admin-user</c> skill
/// uses), so the seed and the auth flow agree on the algorithm.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password. Returns the encoded PHC string.</summary>
    string Hash(string plaintext);

    /// <summary>Verifies a plaintext password against a previously hashed value.</summary>
    /// <returns><c>true</c> if the password matches the hash; <c>false</c> otherwise.</returns>
    bool Verify(string plaintext, string hash);
}

/// <summary>BCrypt-backed <see cref="IPasswordHasher"/>. Work factor 12 (~250ms on a 2026-era server).</summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>BCrypt work factor. 12 is the standard for 2026 — 10 is the minimum recommended, 14 is overkill for web auth.</summary>
    private const int WorkFactor = 12;

    public string Hash(string plaintext) => BCrypt.Net.BCrypt.HashPassword(plaintext, WorkFactor);

    public bool Verify(string plaintext, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(plaintext, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // The hash column had an old / corrupt value (e.g. an empty
            // string from a half-applied migration). Treat as "no match"
            // rather than 500-ing on a typo in a historical row.
            return false;
        }
    }
}
