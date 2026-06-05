namespace SonrisaNews.Domain;

/// <summary>The category of an alert. Determines which <see cref="Filters"/> schema applies.</summary>
public enum AlertType
{
    News = 1,
    Market = 2,
    Disaster = 3,
}

/// <summary>How a match is delivered to a channel.</summary>
public enum DeliveryMode
{
    Realtime = 1,
    Digest15m = 2,
    DigestHourly = 3,
    DigestDaily = 4,
}

/// <summary>The type of a notification channel.</summary>
public enum ChannelType
{
    Email = 1,
    Slack = 2,
}

/// <summary>The category of an upstream data source.</summary>
public enum SourceType
{
    News = 1,
    MarketSymbol = 2,
    Disaster = 3,
}

/// <summary>Status of a user account.</summary>
public enum UserStatus
{
    Active = 1,
    PendingEmailVerification = 2,
    Suspended = 3,
    SoftDeleted = 4,
}

/// <summary>Lifecycle of a <c>Notification</c> row. Pending -> Sent or Failed. Digest batching (wave 8) may add a "Queued" state.</summary>
public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,
}

/// <summary>
/// Helpers for the <see cref="SourceType"/> ↔ <see cref="AlertType"/>
/// conversion. The two enums are kept separate (a future migration
/// may split them), but their values are deliberately aligned: a
/// news source emits News events, a market source emits Market
/// events, a disaster source emits Disaster events. <see cref="MarketSymbol"/>
/// is the source's per-symbol grouping; it maps to a single
/// <see cref="AlertType.Market"/> alert per ticker.
/// </summary>
public static class SourceTypeExtensions
{
    /// <summary>Map a <see cref="SourceType"/> to the alert type its events produce.</summary>
    public static AlertType ToAlertType(this SourceType sourceType) => sourceType switch
    {
        SourceType.News => AlertType.News,
        SourceType.MarketSymbol => AlertType.Market,
        SourceType.Disaster => AlertType.Disaster,
        _ => throw new System.ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown source type."),
    };
}
