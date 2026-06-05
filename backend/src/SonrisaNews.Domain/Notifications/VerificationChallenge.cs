namespace SonrisaNews.Domain.Notifications;

/// <summary>
/// The challenge issued by <see cref="INotificationChannel.StartVerificationAsync"/>.
/// Contains the code the user must provide to <see cref="INotificationChannel.VerifyAsync"/>,
/// plus when the code expires. Channels stamp <see cref="ExpiresAt"/> from <c>IClock.UtcNow</c>.
/// </summary>
/// <param name="Code">
/// The challenge code (e.g. a 6-digit string for email, or a message ID for Slack).
/// The user echoes this back to confirm the destination.
/// </param>
/// <param name="ExpiresAt">
/// UTC instant after which the code is invalid. Per the MVP checklist Q3 default, codes expire
/// 24h after issue. After expiry, the user must call <see cref="INotificationChannel.StartVerificationAsync"/>
/// again to get a fresh code.
/// </param>
public record VerificationChallenge(string Code, DateTimeOffset ExpiresAt);
