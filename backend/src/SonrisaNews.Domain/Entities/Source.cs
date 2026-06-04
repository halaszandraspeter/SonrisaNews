namespace SonrisaNews.Domain.Entities;

/// <summary>
/// An admin-curated upstream (an RSS feed, a market symbol group, a disaster
/// feed). The poller iterates <c>Enabled = true</c> rows. The
/// <see cref="Config"/> JSON holds the type-specific source config
/// (e.g. the feed URL for an RSS source).
/// </summary>
public class Source
{
    public Guid Id { get; set; }

    public SourceType Type { get; set; }

    /// <summary>Admin-displayed label (e.g. <c>Reuters World</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type-specific configuration as a JSON string. Stored as <c>TEXT</c> on SQLite, <c>jsonb</c> on Postgres.</summary>
    public string Config { get; set; } = "{}";

    /// <summary>Master switch. False = the poller skips this source.</summary>
    public bool Enabled { get; set; } = true;

    public DateTimeOffset? LastFetchedAt { get; set; }

    /// <summary>Last fetch error message. Cleared on a successful fetch.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
