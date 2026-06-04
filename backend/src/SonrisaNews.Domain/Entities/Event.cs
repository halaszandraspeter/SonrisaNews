namespace SonrisaNews.Domain.Entities;

/// <summary>
/// A raw upstream event (an RSS item, a USGS earthquake feed entry, a yfinance
/// quote). Dedupe is on <c>(SourceId, ExternalId)</c>; the matcher window
/// query is the composite index on <c>(SourceId, OccurredAt)</c>.
/// </summary>
/// <remarks>
/// <see cref="Type"/> reuses <see cref="AlertType"/>: a News alert matches a
/// News event, a Market alert matches a Market event, a Disaster alert matches
/// a Disaster event. The values are spelled out in the matcher contract
/// (<c>docs/implementation/mvp-checklist.md</c> §2 wave 6).
/// </remarks>
public class Event
{
    public Guid Id { get; set; }

    public Guid SourceId { get; set; }

    /// <summary>The upstream-assigned identifier (RSS <c>&lt;guid&gt;</c>, USGS event id, ticker + timestamp). Unique per source.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public AlertType Type { get; set; }

    /// <summary>Raw upstream payload as a JSON string. Stored as <c>TEXT</c> on SQLite, <c>jsonb</c> on Postgres.</summary>
    public string Payload { get; set; } = "{}";

    /// <summary>UTC time the upstream reported the event happening.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>UTC time our poller persisted the row. Used for the 30-day retention cutoff.</summary>
    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
}
