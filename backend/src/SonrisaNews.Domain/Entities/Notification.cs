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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
