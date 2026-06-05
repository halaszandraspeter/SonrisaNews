namespace SonrisaNews.Domain.Alerts;

/// <summary>
/// Filter shape for a <see cref="Entities.Alert"/> of type <c>News</c>.
/// Stored as the <c>Filters</c> JSON column on the alert; the JSON keys
/// are stable and are the contract between the backend and the
/// frontend's alert editor.
/// </summary>
/// <param name="SourceIds">
/// Optional list of <c>Source.Id</c> values to scope the alert to. Null or
/// empty = match against every enabled news source.
/// </param>
/// <param name="Keyword">
/// Optional single keyword matched against the event title or summary.
/// Null = no keyword filter.
/// </param>
/// <param name="MatchMode">
/// How <paramref name="Keyword"/> is interpreted. Only meaningful when
/// <paramref name="Keyword"/> is non-null. Defaults to <see cref="KeywordMatchMode.All"/>
/// (kept for forward compatibility — a future wave may allow a list of
/// keywords with the same mode).
/// </param>
/// <param name="Tags">
/// Optional list of free-form tags matched against the event's tag set.
/// Null or empty = no tag filter.
/// </param>
public sealed record NewsAlertFilters(
    IReadOnlyList<Guid>? SourceIds,
    string? Keyword,
    KeywordMatchMode MatchMode,
    IReadOnlyList<string>? Tags);
