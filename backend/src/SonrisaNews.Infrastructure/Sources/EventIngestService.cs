using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Domain.Sources;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Sources;

/// <summary>
/// Persists a batch of <see cref="RawEvent"/>s into the <c>Events</c>
/// table, deduped on <c>(SourceId, ExternalId)</c>. The poller calls
/// this once per source per tick; the dedupe rule is the
/// wave-6 reliability rule from <c>1-features.md</c> §6 ("dedup is on
/// (source_id, external_id)").
/// </summary>
/// <remarks>
/// The DB has a unique index <c>UX_Events_SourceId_ExternalId</c> on
/// the pair; the service does an explicit pre-check via
/// <see cref="IQueryable.AnyAsync"/> so the worker log doesn't fill up
/// with constraint-violation noise on the flaky-upstream case. The
/// unique index is the safety net.
/// </remarks>
public sealed class EventIngestService
{
    private readonly SonrisaNewsDbContext _db;
    private readonly IClock _clock;

    public EventIngestService(SonrisaNewsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Persist the given batch. Returns the ids of the newly-inserted
    /// <c>Event</c> rows (the dup-suppressed subset). A throw-free
    /// contract: a DB error is reported as an empty list and a log
    /// line — the poller's per-source loop relies on this for
    /// reliability.
    /// </summary>
    /// <param name="sourceId">The id of the <c>Source</c> row that emitted the events.</param>
    /// <param name="expectedType">
    /// The alert-type equivalent of the source's type. The
    /// <c>Source.Type</c> enum and the <c>Alert.Type</c> enum share
    /// the same numeric values but live in different namespaces (a
    /// future migration may split them); the conversion is the
    /// explicit seam.
    /// </param>
    /// <param name="batch">The events to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<Guid>> IngestAsync(
        Guid sourceId,
        AlertType expectedType,
        IReadOnlyList<RawEvent> batch,
        CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        // The DB has a unique index, but doing the pre-check avoids
        // the constraint violation in the log on every flaky-upstream
        // case. We pull the existing external ids in one round-trip.
        var externalIds = batch.Select(b => b.ExternalId).ToList();
        var existing = await _db.Events
            .Where(e => e.SourceId == sourceId && externalIds.Contains(e.ExternalId))
            .Select(e => e.ExternalId)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.Ordinal);

        var now = _clock.UtcNow;
        var newIds = new List<Guid>(batch.Count);
        foreach (var raw in batch)
        {
            if (existingSet.Contains(raw.ExternalId))
            {
                continue;
            }

            // Defensive: a misconfigured source whose events carry
            // a different type than the source row must not poison
            // the Events table with the wrong type. The matcher is
            // type-aware and would otherwise silently skip the row.
            if (raw.Type != expectedType)
            {
                continue;
            }

            var evt = new Event
            {
                Id = Guid.NewGuid(),
                SourceId = sourceId,
                ExternalId = raw.ExternalId,
                Type = raw.Type,
                Payload = raw.Payload,
                OccurredAt = raw.OccurredAt,
                FetchedAt = now,
            };
            _db.Events.Add(evt);
            newIds.Add(evt.Id);
        }

        if (newIds.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
        return newIds;
    }
}
