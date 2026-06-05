namespace SonrisaNews.Domain.Sources;

/// <summary>
/// A single item produced by an <see cref="IDataSource"/>. The poller turns
/// the list returned by <see cref="IDataSource.FetchAsync"/> into
/// <c>Event</c> rows, deduped by <c>(SourceId, ExternalId)</c>.
/// </summary>
/// <param name="ExternalId">
/// The upstream-assigned identifier (an RSS <c>&lt;guid&gt;</c>, a USGS
/// event id, a ticker + timestamp). Stable; the poller's dedupe
/// <c>UX_Events_SourceId_ExternalId</c> index is on this value, so a
/// flaky upstream that re-emits the same item never inserts a duplicate.
/// </param>
/// <param name="Type">The domain type of the event. Matches an <c>AlertType</c> value.</param>
/// <param name="Payload">
/// The raw upstream payload, JSON-serialised. The matcher reads the
/// fields it cares about (e.g. <c>title</c>, <c>summary</c>, <c>tags</c>)
/// out of this string via <c>JsonDocument</c>. Storing the raw payload
/// keeps the matcher honest (no hand-rolled "fields" on this record)
/// and gives the activity feed something to show the user.
/// </param>
/// <param name="OccurredAt">UTC time the upstream reported the event happening.</param>
public sealed record RawEvent(
    string ExternalId,
    Domain.AlertType Type,
    string Payload,
    DateTimeOffset OccurredAt);
