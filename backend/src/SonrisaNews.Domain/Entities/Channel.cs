namespace SonrisaNews.Domain.Entities;

/// <summary>
/// A user's connection to an external destination (an email inbox, a Slack
/// webhook, etc.). Notifications are routed through channels; a channel must
/// be <see cref="Verified"/> before the dispatcher will send to it.
/// </summary>
/// <remarks>
/// <see cref="QuietHoursStart"/> and <see cref="QuietHoursEnd"/> are nullable
/// local-time <see cref="TimeOnly"/> values — null means "no quiet hours",
/// which is the MVP default per <c>1-features.md</c> §2.4. The values are
/// stored as <c>TIME</c> in SQLite/Postgres and interpreted in the user's
/// <c>User.TimeZone</c>.
/// </remarks>
public class Channel
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ChannelType Type { get; set; }

    /// <summary>The address the user entered (e.g. <c>ada@example.com</c> or a Slack incoming-webhook URL).</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Set to true only after the user has completed the verification flow for this destination.</summary>
    public bool Verified { get; set; }

    /// <summary>Local-time start of the quiet window. Null = no quiet hours.</summary>
    public TimeOnly? QuietHoursStart { get; set; }

    /// <summary>Local-time end of the quiet window. May wrap past midnight (e.g. 22:00–07:00).</summary>
    public TimeOnly? QuietHoursEnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
