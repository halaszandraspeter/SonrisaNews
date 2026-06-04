namespace SonrisaNews.Domain.Entities;

/// <summary>
/// A record that an event fired an alert. The audit trail of "this event
/// fired this alert". A <c>Match</c> is unique per <c>(AlertId, EventId)</c>
/// — the dispatcher relies on this to be idempotent under retries
/// (<c>1-features.md</c> §6 reliability rule).
/// </summary>
public class Match
{
    public Guid Id { get; set; }

    public Guid AlertId { get; set; }

    public Guid EventId { get; set; }

    /// <summary>UTC time the matcher emitted the row.</summary>
    public DateTimeOffset FiredAt { get; set; } = DateTimeOffset.UtcNow;
}
