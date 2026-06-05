namespace SonrisaNews.Domain.Entities;

/// <summary>
/// A person who signs in to Sonrisa News. The entity is the root of the domain;
/// channels, alerts, notifications, and audit log entries all reference a User.
/// </summary>
/// <remarks>
/// <para>
/// Field set is the MVP minimum from <c>docs/roadmap/1-features.md</c> §4
/// with wave-3 additions (<c>MustChangePassword</c>) applied via the
/// <c>AddRbacCatalog</c> migration.
/// </para>
/// <para>
/// <b>User rule 2026-06-05</b>: the <c>Role</c> column was dropped in
/// wave 3. A user's role membership is a row in <c>UserRoles</c>; the
/// <c>RbacPolicyHandler</c> resolves authorization through the
/// <c>UserRoles ⨝ RolePermissions ⨝ Permissions</c> join. There is no
/// <c>UserRole</c> property on this entity any more.
/// </para>
/// <para>
/// Wave 8 (cleanup) reads <see cref="DeletedAt"/> to enforce the 30-day
/// soft-delete grace period.
/// </para>
/// </remarks>
public class User
{
    public Guid Id { get; set; }

    /// <summary>Email used for sign-in and notifications. Unique, case-insensitive.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Argon2id hash of the user's password. Never stored in plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>User-chosen display name. Falls back to the local part of the email for anonymous mentions.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>IANA time-zone id (e.g. <c>Europe/Budapest</c>). Used to render digest times in the user's local clock.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Account lifecycle state. New users land in <see cref="UserStatus.PendingEmailVerification"/>.</summary>
    public UserStatus Status { get; set; } = UserStatus.PendingEmailVerification;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>UTC timestamp at which the user (or an admin) requested deletion. Null = active. Hard-delete happens 30 days later (wave 8 cleanup).</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// True if the user must rotate their password on the next sign-in.
    /// Set by <c>AdminSeeder</c> for the bootstrap admin (the env-var
    /// password must be replaced before the account is usable long-term)
    /// and by the <c>ForgotPassword</c> flow.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
