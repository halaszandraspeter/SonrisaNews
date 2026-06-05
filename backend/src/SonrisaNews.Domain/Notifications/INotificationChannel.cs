namespace SonrisaNews.Domain.Notifications;

/// <summary>
/// A pluggable notification channel — an external destination where alerts can be sent.
/// Implementations: <see cref="EmailChannel"/>, <see cref="SlackChannel"/>, and future SMS/webhook/push.
///
/// Registered in DI with a key: <c>AddKeyedScoped&lt;INotificationChannel, EmailChannel&gt;("email")</c>.
/// The dispatcher resolves the right channel via key.
/// </summary>
public interface INotificationChannel
{
    /// <summary>The channel type identifier (e.g. "email", "slack"). Matches <see cref="ChannelType"/>.</summary>
    string Type { get; }

    /// <summary>
    /// Begin verification of a channel destination.
    /// Issues a challenge (e.g. a 6-digit code for email, or a test message for Slack).
    /// Returns the challenge to be confirmed by the user in a second call to <see cref="VerifyAsync"/>.
    /// </summary>
    /// <param name="destination">The user-supplied destination (e.g. email address, Slack webhook URL).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The challenge the user must echo back (e.g. the code sent to their email).</returns>
    Task<VerificationChallenge> StartVerificationAsync(string destination, CancellationToken ct);

    /// <summary>
    /// Verify a channel destination by checking the code issued in <see cref="StartVerificationAsync"/>.
    /// </summary>
    /// <param name="destination">The destination to verify.</param>
    /// <param name="code">The code the user received (e.g. from email or Slack message).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the code matches; false otherwise. No exception is thrown on mismatch.</returns>
    Task<bool> VerifyAsync(string destination, string code, CancellationToken ct);

    /// <summary>
    /// Send a notification to this channel.
    /// Must not throw. All errors are wrapped in <see cref="SendResult.Failure"/>.
    /// </summary>
    /// <param name="payload">The notification content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or a detailed failure result.</returns>
    Task<SendResult> SendAsync(NotificationPayload payload, CancellationToken ct);
}
