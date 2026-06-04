namespace SonrisaNews.Domain.Entities;

/// <summary>One-shot, single-user email verification token. Deleted on use (set <see cref="UsedAt"/>).</summary>
public class EmailVerification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Opaque token sent in the verification email. The user pastes the code or clicks the link.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
