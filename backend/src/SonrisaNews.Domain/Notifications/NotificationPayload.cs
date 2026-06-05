namespace SonrisaNews.Domain.Notifications;

/// <summary>
/// The payload for a notification being sent to a channel.
/// Contains all the information a channel needs to compose and deliver the message.
/// </summary>
public record NotificationPayload(
    Guid NotificationId,
    Guid AlertId,
    string AlertName,
    Guid ChannelId,
    string Destination,
    string MatchSummary,
    DateTimeOffset OccurredAt
);
