namespace SonrisaNews.Domain.Entities;

/// <summary>
/// Join row binding an <see cref="Alert"/> to a <see cref="Channel"/> with a
/// <see cref="DeliveryMode"/>. The (AlertId, ChannelId) pair is the primary
/// key — the channel-mode matrix is unique per row.
/// </summary>
public class AlertChannelMode
{
    public Guid AlertId { get; set; }
    public Guid ChannelId { get; set; }

    public DeliveryMode Mode { get; set; } = DeliveryMode.Realtime;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
