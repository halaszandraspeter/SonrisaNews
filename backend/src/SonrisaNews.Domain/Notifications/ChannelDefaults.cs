namespace SonrisaNews.Domain.Notifications;

/// <summary>
/// Constants shared by all <see cref="INotificationChannel"/> implementations.
/// </summary>
public static class ChannelDefaults
{
    /// <summary>
    /// How long a verification code remains valid after <see cref="INotificationChannel.StartVerificationAsync"/>.
    /// Per the MVP checklist Q3 default (24h). After expiry, the user must request a new code.
    /// </summary>
    public static readonly TimeSpan VerificationTtl = TimeSpan.FromHours(24);
}
