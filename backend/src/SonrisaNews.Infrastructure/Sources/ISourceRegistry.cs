using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Domain.Sources;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Sources;

/// <summary>
/// Resolves the list of <see cref="IDataSource"/>s the worker should
/// poll on a given tick. Production resolves from the
/// <c>Sources</c> table (one row per admin-curated source); the
/// <c>NewsPoller</c> tests resolve from a test-supplied list so the
/// HTTP / poll path is not coupled to the DB shape.
/// </summary>
/// <remarks>
/// The interface lives in Infrastructure (not Domain) because the
/// resolution is the worker's concern: the domain doesn't know how
/// the worker schedules. The <see cref="DbSourceRegistry"/>
/// implementation is the production seam.
/// </remarks>
public interface ISourceRegistry
{
    /// <summary>All enabled sources. The order is the order the poller will iterate.</summary>
    Task<IReadOnlyList<IDataSource>> GetEnabledAsync(CancellationToken ct);
}

/// <summary>
/// Production <see cref="ISourceRegistry"/>: reads the enabled rows
/// from the <c>Sources</c> table and instantiates the right
/// <see cref="IDataSource"/> per row. The <c>Source.Config</c> JSON
/// holds the per-source config (e.g. the feed URL for an
/// <see cref="RssSource"/>).
/// </summary>
/// <remarks>
/// The registry is created fresh on each call to
/// <see cref="GetEnabledAsync"/>. Admin enable/disable actions take
/// effect on the next tick.
/// </remarks>
public sealed class DbSourceRegistry : ISourceRegistry
{
    private readonly SonrisaNewsDbContext _db;
    private readonly IRssSourceFactory _rssFactory;
    private readonly ILogger<DbSourceRegistry> _logger;

    public DbSourceRegistry(
        SonrisaNewsDbContext db,
        IRssSourceFactory rssFactory,
        ILogger<DbSourceRegistry> logger)
    {
        _db = db;
        _rssFactory = rssFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IDataSource>> GetEnabledAsync(CancellationToken ct)
    {
        var rows = await _db.Sources
            .AsNoTracking()
            .Where(s => s.Enabled)
            .ToListAsync(ct);

        var list = new List<IDataSource>(rows.Count);
        foreach (var row in rows)
        {
            if (TryBuild(row) is { } source)
            {
                list.Add(source);
            }
        }
        return list;
    }

    private IDataSource? TryBuild(Source row) => row.Type switch
    {
        SourceType.News => _rssFactory.TryCreate(row),
        // Market and Disaster land in waves 7 and 8. For now, the
        // worker skips unknown source types. We log a warning so a
        // future admin who enables a Disaster source before wave 8
        // lands gets a clear signal in the log instead of a silent
        // skip (the original SHOULD item from the wave-6 review).
        _ => LogSkipWithReason(row),
    };

    /// <summary>
    /// Decide whether a non-News source type is "not yet implemented
    /// in this build" (transient — wave 7/8 will add it; the row is
    /// fine) or "out of range / data corruption" (permanent — the
    /// <c>SourceType</c> enum has no value this high, which means
    /// the row was written by a future build and rolled back, or
    /// someone hand-edited the DB). The log message distinguishes
    /// the two so a future operator reading the log can tell which
    /// remediation applies.
    /// </summary>
    private IDataSource? LogSkipWithReason(Source row)
    {
        var isKnownButUnimplemented =
            row.Type == SourceType.MarketSymbol || row.Type == SourceType.Disaster;
        if (isKnownButUnimplemented)
        {
            _logger.LogWarning(
                "Source {SourceId} ({Name}) has type {Type} which is not implemented in this build yet (a future wave will add it); skipping",
                row.Id, row.Name, row.Type);
        }
        else
        {
            _logger.LogWarning(
                "Source {SourceId} ({Name}) has an unknown type {Type} (out of range or data corruption); skipping",
                row.Id, row.Name, row.Type);
        }
        return null;
    }
}

/// <summary>
/// Builds an <see cref="RssSource"/> for a <c>Source</c> row. The
/// <c>Config</c> JSON must contain a <c>feedUrl</c> string; rows
/// missing the field are skipped (logged at the call site).
/// </summary>
public interface IRssSourceFactory
{
    RssSource? TryCreate(Source row);
}

public sealed class RssSourceFactory : IRssSourceFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssSource> _logger;

    public RssSourceFactory(IHttpClientFactory httpClientFactory, ILogger<RssSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public RssSource? TryCreate(Source row)
    {
        var feedUrl = ReadFeedUrl(row);
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning(
                "Source {SourceId} ({Name}) is enabled but has no feedUrl in its Config; skipping",
                row.Id, row.Name);
            return null;
        }
        return new RssSource(_httpClientFactory, feedUrl, row.Id.ToString(), _logger);
    }

    private static string? ReadFeedUrl(Source row)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(row.Config);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("feedUrl", out var prop)
                && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Malformed Config — fall through to the "no feedUrl" branch.
        }
        return null;
    }
}
