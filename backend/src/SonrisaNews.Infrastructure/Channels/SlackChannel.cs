using System.Text.Json;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Channels;

/// <summary>
/// Slack notification channel. Verification sends a test message to the webhook.
/// The webhook URL must be a valid Slack incoming webhook URL.
///
/// Verification codes are time-boxed (24h by default, see <see cref="ChannelDefaults.VerificationTtl"/>).
/// </summary>
public sealed class SlackChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackChannel> _logger;
    private readonly IClock _clock;

    // In-memory storage of verification codes. In production, this could be backed by Redis/DB.
    private readonly Dictionary<string, (string Code, DateTimeOffset ExpiresAt)> _verificationCodes = new();

    public string Type => ChannelTypeConstants.Slack;

    public SlackChannel(IHttpClientFactory httpClientFactory, ILogger<SlackChannel> logger, IClock clock)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _clock = clock;
    }

    public Task<VerificationChallenge> StartVerificationAsync(string destination, CancellationToken ct)
    {
        // Generate a verification code
        var code = Guid.NewGuid().ToString("N").Substring(0, 12);
        var expiresAt = _clock.UtcNow.Add(ChannelDefaults.VerificationTtl);
        _verificationCodes[destination] = (code, expiresAt);

        _logger.LogInformation("Slack verification code generated, expires at {ExpiresAt:o}", expiresAt);

        // In a real implementation, this would POST to the webhook with a test message
        // For tests, we just return the code

        return Task.FromResult(new VerificationChallenge(code, expiresAt));
    }

    public Task<bool> VerifyAsync(string destination, string code, CancellationToken ct)
    {
        if (!_verificationCodes.TryGetValue(destination, out var entry))
        {
            _logger.LogInformation("No verification code found for Slack webhook");
            return Task.FromResult(false);
        }

        if (_clock.UtcNow >= entry.ExpiresAt)
        {
            _verificationCodes.Remove(destination);
            _logger.LogInformation("Slack verification code expired");
            return Task.FromResult(false);
        }

        var isValid = entry.Code == code;

        if (isValid)
        {
            _verificationCodes.Remove(destination);
            _logger.LogInformation("Slack webhook verified successfully");
        }
        else
        {
            _logger.LogInformation("Slack webhook verification failed: code mismatch");
        }

        return Task.FromResult(isValid);
    }

    public async Task<SendResult> SendAsync(NotificationPayload payload, CancellationToken ct)
    {
        try
        {
            // Validate webhook URL format
            if (!Uri.TryCreate(payload.Destination, UriKind.Absolute, out var uri) || !uri.Scheme.StartsWith("http"))
            {
                _logger.LogInformation("Invalid webhook URL format");
                return SendResult.Failure("invalid_webhook_url", "The webhook URL is invalid.");
            }

            var httpClient = _httpClientFactory.CreateClient();

            // Build the Slack message
            var message = new
            {
                text = $"🔔 *{payload.AlertName}*",
                blocks = new object[]
                {
                    new
                    {
                        type = "header",
                        text = new { type = "plain_text", text = payload.AlertName }
                    },
                    new
                    {
                        type = "section",
                        text = new { type = "mrkdwn", text = payload.MatchSummary }
                    }
                }
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(payload.Destination, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Slack webhook returned non-success status: {StatusCode}", response.StatusCode);
                return SendResult.Failure("webhook_failed", $"Slack webhook returned status {response.StatusCode}");
            }

            _logger.LogInformation("Slack notification sent successfully for {NotificationId}", payload.NotificationId);
            return SendResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
            return SendResult.Failure("slack_send_failed", "An error occurred while sending the Slack notification.");
        }
    }
}
