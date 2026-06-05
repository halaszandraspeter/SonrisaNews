namespace SonrisaNews.Domain.Alerts;

/// <summary>
/// How a list of keywords is matched against an event's title or content.
/// Used by <see cref="NewsAlertFilters"/>.
/// </summary>
public enum KeywordMatchMode
{
    /// <summary>All keywords must be present (logical AND).</summary>
    All = 1,

    /// <summary>Any keyword is enough (logical OR).</summary>
    Any = 2,
}
