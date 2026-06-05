using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Sources;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Sources;

/// <summary>
/// A generic RSS 2.0 / Atom data source. Fetches a single feed URL on
/// each call to <see cref="FetchAsync"/> and converts each <c>&lt;item&gt;</c>
/// into a <see cref="RawEvent"/> with a stable <see cref="RawEvent.ExternalId"/>.
///
/// The <c>Id</c> is the row id in the <c>Sources</c> table; the admin
/// seeds one <see cref="Source"/> row per feed. The poller iterates
/// all enabled <c>Source</c> rows, instantiates a typed client per
/// row, and calls <see cref="FetchAsync"/>.
/// </summary>
/// <remarks>
/// The source is intentionally simple — no caching, no dedupe
/// (dedupe is the poller's job, via <c>UX_Events_SourceId_ExternalId</c>).
/// Exceptions from the upstream are caught and converted to an empty
/// list per the <see cref="IDataSource"/> contract.
/// </remarks>
public sealed class RssSource : IDataSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _feedUrl;
    private readonly ILogger<RssSource>? _logger;

    /// <summary>Builds a source that polls the given feed URL.</summary>
    /// <param name="httpClientFactory">Injected factory. The source names its client
    /// <c>"rss"</c> so future Polly retry policies can be layered per-source.</param>
    /// <param name="feedUrl">The absolute URL of the feed. Must be HTTPS in prod.</param>
    /// <param name="id">Stable id — matches the <c>Source.Id</c> row in the DB.</param>
    public RssSource(IHttpClientFactory httpClientFactory, string feedUrl, string id)
    {
        _httpClientFactory = httpClientFactory;
        _feedUrl = feedUrl;
        Id = id;
    }

    /// <summary>Constructor overload for tests that want to inject a logger.</summary>
    public RssSource(IHttpClientFactory httpClientFactory, string feedUrl, string id, ILogger<RssSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _feedUrl = feedUrl;
        Id = id;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public SourceType Type => SourceType.News;

    /// <inheritdoc />
    public TimeSpan PollInterval => TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RawEvent>> FetchAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("rss");
        string body;
        try
        {
            using var response = await client.GetAsync(_feedUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "RssSource {SourceId} got {Status} from {Url}; returning empty list",
                    Id, (int)response.StatusCode, _feedUrl);
                return Array.Empty<RawEvent>();
            }
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The poller's per-source loop depends on this never throwing.
            // A transient network blip must surface as an empty list and a
            // log line, not as a worker crash.
            _logger?.LogWarning(ex,
                "RssSource {SourceId} fetch from {Url} threw {Type}; returning empty list",
                Id, _feedUrl, ex.GetType().Name);
            return Array.Empty<RawEvent>();
        }

        try
        {
            return ParseRss(body).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "RssSource {SourceId} feed body from {Url} did not parse as RSS/Atom; returning empty list",
                Id, _feedUrl);
            return Array.Empty<RawEvent>();
        }
    }

    /// <summary>
    /// Parse a RSS 2.0 (preferred) or Atom feed into a list of
    /// <see cref="RawEvent"/>. RSS is the documented contract; Atom is
    /// parsed as a fallback because many "RSS-ish" feeds (Medium, GitHub
    /// releases, etc.) are actually Atom. Both <c>&lt;guid&gt;</c> and
    /// <c>&lt;link&gt;</c> are accepted as the external id; we prefer
    /// the guid for stability.
    /// </summary>
    internal static IEnumerable<RawEvent> ParseRss(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            yield break;
        }

        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null)
        {
            yield break;
        }

        // RSS 2.0
        if (string.Equals(root.Name.LocalName, "rss", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in root.Descendants("item"))
            {
                if (TryParseRssItem(item, out var evt))
                {
                    yield return evt;
                }
            }
            yield break;
        }

        // Atom (fallback)
        if (string.Equals(root.Name.LocalName, "feed", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in root.Descendants().Where(e => e.Name.LocalName == "entry"))
            {
                if (TryParseAtomEntry(entry, out var evt))
                {
                    yield return evt;
                }
            }
        }
    }

    private static bool TryParseRssItem(XElement item, out RawEvent evt)
    {
        var title = (string?)item.Element("title") ?? string.Empty;
        var description = (string?)item.Element("description") ?? string.Empty;
        var link = (string?)item.Element("link") ?? string.Empty;
        var guid = (string?)item.Element("guid");
        var pubDateRaw = (string?)item.Element("pubDate");

        var externalId = !string.IsNullOrWhiteSpace(guid) ? guid : link;
        if (string.IsNullOrWhiteSpace(externalId))
        {
            evt = default!;
            return false;
        }

        var occurredAt = ParseRfc822Date(pubDateRaw);
        var payload = SerializePayload(title, description, link, guid, pubDateRaw);
        evt = new RawEvent(externalId, AlertType.News, payload, occurredAt);
        return true;
    }

    private static bool TryParseAtomEntry(XElement entry, out RawEvent evt)
    {
        var title = (string?)entry.Element("{http://www.w3.org/2005/Atom}title") ?? string.Empty;
        var summary = (string?)entry.Element("{http://www.w3.org/2005/Atom}summary") ?? string.Empty;
        var linkEl = entry.Elements("{http://www.w3.org/2005/Atom}link")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase))
            ?? entry.Elements("{http://www.w3.org/2005/Atom}link").FirstOrDefault();
        var link = linkEl is not null ? ((string?)linkEl.Attribute("href") ?? string.Empty) : string.Empty;
        var id = (string?)entry.Element("{http://www.w3.org/2005/Atom}id") ?? link;
        var updatedRaw = (string?)entry.Element("{http://www.w3.org/2005/Atom}updated")
            ?? (string?)entry.Element("{http://www.w3.org/2005/Atom}published");

        if (string.IsNullOrWhiteSpace(id))
        {
            evt = default!;
            return false;
        }

        var occurredAt = ParseIso8601Date(updatedRaw);
        var payload = SerializePayload(title, summary, link, id, updatedRaw);
        evt = new RawEvent(id, AlertType.News, payload, occurredAt);
        return true;
    }

    /// <summary>
    /// RSS pubDate is RFC 822 ("Fri, 05 Jun 2026 12:00:00 GMT").
    /// Falls back to <see cref="DateTimeOffset.UtcNow"/> on parse
    /// failure — a malformed date is not worth skipping the event.
    /// </summary>
    private static DateTimeOffset ParseRfc822Date(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

    private static DateTimeOffset ParseIso8601Date(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

    private static string SerializePayload(
        string title, string summary, string link, string? guid, string? pubDate) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            summary,
            link,
            guid,
            pubDate,
        });
}
