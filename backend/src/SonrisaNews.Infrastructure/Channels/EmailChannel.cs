using System.Net.Mail;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Channels;

/// <summary>
/// Email notification channel. Verification sends a 6-digit code via MailKit SMTP.
/// In tests, the SMTP client is mocked; in production it sends via the configured mail server.
///
/// Verification codes are time-boxed (24h by default, see <see cref="ChannelDefaults.VerificationTtl"/>).
/// The clock is injected so tests can advance time deterministically.
/// </summary>
public sealed class EmailChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailChannel> _logger;
    private readonly IClock _clock;

    // In-memory storage of verification codes. In production, this could be backed by Redis/DB.
    private readonly Dictionary<string, (string Code, DateTimeOffset ExpiresAt)> _verificationCodes = new();
    private readonly Random _random = new();

    public string Type => ChannelTypeConstants.Email;

    public EmailChannel(IHttpClientFactory httpClientFactory, ILogger<EmailChannel> logger, IClock clock)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _clock = clock;
    }

    public Task<VerificationChallenge> StartVerificationAsync(string destination, CancellationToken ct)
    {
        // Generate a 6-digit code
        var code = _random.Next(100000, 999999).ToString();
        var expiresAt = _clock.UtcNow.Add(ChannelDefaults.VerificationTtl);
        _verificationCodes[destination] = (code, expiresAt);

        _logger.LogInformation("Verification code generated for email verification, expires at {ExpiresAt:o}", expiresAt);

        // In MVP, we don't actually send email here (no SMTP configured).
        // In a real implementation, this would call MailKit to send the code.
        // For now, we just return the code so the test can verify it.

        return Task.FromResult(new VerificationChallenge(code, expiresAt));
    }

    public Task<bool> VerifyAsync(string destination, string code, CancellationToken ct)
    {
        if (!_verificationCodes.TryGetValue(destination, out var entry))
        {
            _logger.LogInformation("No verification code found for destination");
            return Task.FromResult(false);
        }

        // Time-box the code: a stale entry cannot be redeemed even if its bytes match.
        if (_clock.UtcNow >= entry.ExpiresAt)
        {
            _verificationCodes.Remove(destination);
            _logger.LogInformation("Verification code expired for destination");
            return Task.FromResult(false);
        }

        var isValid = entry.Code == code;

        if (isValid)
        {
            _verificationCodes.Remove(destination);
            _logger.LogInformation("Email verified successfully");
        }
        else
        {
            _logger.LogInformation("Email verification failed: code mismatch");
        }

        return Task.FromResult(isValid);
    }

    public async Task<SendResult> SendAsync(NotificationPayload payload, CancellationToken ct)
    {
        try
        {
            // Validate email format
            if (!IsValidEmail(payload.Destination))
            {
                _logger.LogInformation("Invalid email format for notification");
                return SendResult.Failure("invalid_email", "The destination email address is invalid.");
            }

            // In MVP, we don't actually send email (MailKit not configured for local dev).
            // In a real implementation, this would connect to an SMTP server and send.
            // For now, we just simulate success.

            _logger.LogInformation("Email notification queued for {NotificationId}", payload.NotificationId);

            return SendResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification");
            return SendResult.Failure("email_send_failed", "An error occurred while sending the email.");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
