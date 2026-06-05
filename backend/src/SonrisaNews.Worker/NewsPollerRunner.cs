using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Domain.Sources;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Infrastructure.Sources;
using SonrisaNews.Shared;

namespace SonrisaNews.Worker;

/// <summary>
/// Inner runner that encapsulates the "fetch → ingest → match" loop
/// for a single tick. Public so the integration tests can construct
/// it with a shared in-memory <see cref="SonrisaNewsDbContext"/> and
/// fakes for <see cref="ISourceRegistry"/> and the matcher. The
/// hosted service <see cref="NewsPoller"/> wraps this in the DI
/// scope-management + retry loop.
/// </summary>
/// <remarks>
/// One runner per tick. The runner is stateless; reuse across ticks
/// is safe as long as the constructor dependencies are reusable
/// (the <see cref="SonrisaNewsDbContext"/> is scoped per DI scope or
/// per test, the registry is a singleton, etc.).
/// </remarks>
public sealed class NewsPollerRunner
{
    private readonly SonrisaNewsDbContext _db;
    private readonly IClock _clock;
    private readonly ISourceRegistry _registry;
    private readonly EventIngestService _ingest;
    private readonly NewsMatcher _matcher;
    private readonly ILogger<NewsPollerRunner> _logger;

    public NewsPollerRunner(
        SonrisaNewsDbContext db,
        IClock clock,
        ISourceRegistry registry,
        EventIngestService ingest,
        NewsMatcher matcher,
        ILogger<NewsPollerRunner> logger)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _ingest = ingest;
        _matcher = matcher;
        _logger = logger;
    }

    /// <summary>
    /// Run a single poll pass. The hosted service calls this via the
    /// DI scope; the integration tests call it directly on a shared
    /// <see cref="SonrisaNewsDbContext"/>. The method is idempotent —
    /// a re-run on the same tick state is a no-op for the source
    /// fetches (each <see cref="IDataSource"/> is stateless) and a
    /// no-op for the matcher (UX_Matches_AlertId_EventId + the
    /// pre-check).
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var sources = await _registry.GetEnabledAsync(ct);
        _logger.LogDebug("NewsPoller: polling {Count} enabled source(s)", sources.Count);

        foreach (var source in sources)
        {
            if (ct.IsCancellationRequested) return;
            await PollOneAsync(source, ct);
        }
    }

    private async Task PollOneAsync(IDataSource source, CancellationToken ct)
    {
        IReadOnlyList<RawEvent> raw;
        try
        {
            raw = await source.FetchAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The IDataSource contract is "do not throw"; this is
            // defense-in-depth in case a future source misbehaves.
            _logger.LogWarning(ex,
                "NewsPoller: source {SourceId} threw {Type}; recording last-error and continuing",
                source.Id, ex.GetType().Name);
            await RecordSourceErrorAsync(source.Id, ex.Message, ct);
            return;
        }

        if (raw.Count == 0)
        {
            // Successful fetch with no events — clear any prior
            // last-error so the admin health page stops flagging this
            // source as failing.
            await MutateSourceAsync(source.Id, row => { row.LastError = null; }, ct);
            return;
        }

        var newIds = await _ingest.IngestAsync(ParseSourceId(source.Id), source.Type.ToAlertType(), raw, ct);
        // Successful fetch + ingest — clear the last-error and stamp
        // LastFetchedAt so the admin health page sees the green state.
        await MutateSourceAsync(source.Id, row =>
        {
            row.LastError = null;
        }, ct);

        // Run the matcher for each newly-inserted event. The
        // matcher is idempotent so a re-run of this loop on
        // already-matched events is a no-op.
        // Cost note: this loop is N events × M alerts. The matcher
        // re-loads the enabled News alert set on every call. At
        // MVP scale (a few dozen alerts, a few events per tick) the
        // N+1 round-trips are negligible. Wave 7 (market poller)
        // should pre-load the alert set once per tick and pass it
        // into a matcher overload; the current shape is the wave-6
        // scope minimum.
        foreach (var eventId in newIds)
        {
            if (ct.IsCancellationRequested) return;
            // AsNoTracking: the matcher reads the event but doesn't
            // mutate it, so we don't need EF change tracking on it.
            var evt = await _db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventId, ct);
            if (evt is null) continue;
            await _matcher.RunForEventAsync(evt, ct);
        }
    }

    /// <summary>
    /// Apply <paramref name="mutate"/> to the <c>Source</c> row whose
    /// string id parses to a Guid, then stamp <c>LastFetchedAt</c>
    /// to <see cref="IClock.UtcNow"/> in the same SaveChanges. The
    /// "LastFetchedAt + optional extra field" is the canonical stamp
    /// shape; both the happy-path (clear last-error) and the
    /// failure-path (record last-error) call this with their own
    /// mutate callback so the "load + save" lifecycle lives in
    /// one place.
    /// </summary>
    private async Task MutateSourceAsync(
        string sourceId,
        Action<Source> mutate,
        CancellationToken ct)
    {
        if (!Guid.TryParse(sourceId, out var sourceGuid))
        {
            return;
        }
        var row = await _db.Sources.FirstOrDefaultAsync(s => s.Id == sourceGuid, ct);
        if (row is null) return;
        mutate(row);
        row.LastFetchedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private Task RecordSourceErrorAsync(string sourceId, string message, CancellationToken ct) =>
        MutateSourceAsync(sourceId, row => { row.LastError = message; }, ct);

    private static Guid ParseSourceId(string sourceId) =>
        Guid.TryParse(sourceId, out var id) ? id : Guid.Empty;
}
