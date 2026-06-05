namespace SonrisaNews.Domain.Entities;

/// <summary>
/// The actual delivery record: a Match routed to a Channel in a particular
/// <see cref="DeliveryMode"/>. The activity feed reads this table; the cleanup
/// service prunes it after 30 days (1-features.md §6 retention).
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid AlertId { get; set; }

    public Guid MatchId { get; set; }

    public Guid ChannelId { get; set; }

    public DeliveryMode Mode { get; set; }

    /// <summary>Lifecycle: <see cref="NotificationStatus.Pending"/> -> <see cref="NotificationStatus.Sent"/> | <see cref="NotificationStatus.Failed"/>. Other states are reserved for digest batching (wave 8).</summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Last delivery error. Cleared on a successful retry.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Idempotency key for "don't double-send the same match to the same channel".
    /// Default = <c>Match.Id</c> (per the MVP checklist Q8 default). The dispatcher
    /// (wave 8) looks up a row by <c>(ChannelId, DedupeKey)</c> before sending —
    /// a re-run after a crash hits the index and skips the send.
    /// Unique per channel: two notifications to the same channel with the same
    /// key are treated as the same logical send.
    /// </summary>
    public Guid DedupeKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
