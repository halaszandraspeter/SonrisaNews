namespace SonrisaNews.Domain.Entities;

/// <summary>
/// A person who signs in to Sonrisa News. The entity is the root of the domain;
/// channels, alerts, notifications, and audit log entries all reference a User.
/// </summary>
/// <remarks>
/// Field set is the MVP minimum from <c>docs/roadmap/1-features.md</c> §4.
/// Wave 3 (auth) wires ASP.NET Core Identity and JWT; the password hash column
/// is what the identity store will use. Wave 8 (cleanup) reads
/// <see cref="DeletedAt"/> to enforce the 30-day soft-delete grace period.
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

    /// <summary>RBAC role. Drives authorization checks. Seeded from <c>rbac_policy.csv</c>.</summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>IANA time-zone id (e.g. <c>Europe/Budapest</c>). Used to render digest times in the user's local clock.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Account lifecycle state. New users land in <see cref="UserStatus.PendingEmailVerification"/>.</summary>
    public UserStatus Status { get; set; } = UserStatus.PendingEmailVerification;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>UTC timestamp at which the user (or an admin) requested deletion. Null = active. Hard-delete happens 30 days later (wave 8 cleanup).</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
