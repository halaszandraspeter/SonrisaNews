using Microsoft.Extensions.Logging;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Sends transactional email. The MVP default is a no-op
/// <c>LoggingEmailSender</c> (writes the body to a structured log line);
/// a real SMTP implementation lands in wave 4 alongside the channel
/// abstraction. Tests use a fake to capture sent messages.
/// </summary>
public interface IEmailSender
{
    /// <summary>Send a single message. Implementations must throw on permanent failures (bad address) and return normally on transient ones (timeouts).</summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Plain-text subject.</param>
    /// <param name="body">Plain-text body. HTML is wave 9 (React Email).</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>Logs the email payload instead of sending it. Use in dev when no SMTP is configured.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        // Structured properties, no PII in the subject/body — just the
        // recipient address (which is the PII, but it's also necessary to
        // verify the email was routed correctly).
        logger.LogInformation(
            "Email (dev): to={To} subject={Subject} bodyChars={BodyLength}",
            to, subject, body.Length);
        return Task.CompletedTask;
    }
}
