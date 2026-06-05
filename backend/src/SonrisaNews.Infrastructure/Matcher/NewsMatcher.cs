using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Matcher;

/// <summary>
/// The news-alert matcher. Evaluates a <see cref="NewsAlertFilters"/>
/// against an <see cref="Event"/> and decides whether the event would
/// fire the alert. When the answer is yes, the matcher inserts a
/// <c>Match</c> row — the audit trail the dispatcher (wave 8) reads.
///
/// The matcher is type-aware: a Market alert is not evaluated against
/// a News event (and vice versa). The <c>Alert.Type</c> and
/// <c>Event.Type</c> values must match.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotency.</b> The unique index
/// <c>UX_Matches_AlertId_EventId</c> is the DB-level guard against
/// re-firing the same alert on the same event. The matcher also
/// pre-checks the rows to avoid spamming the log on every duplicate.
/// </para>
/// <para>
/// <b>Empty filter = match all.</b> A <c>{}</c> News filter is
/// interpreted as "no filter — match every news event". This is the
/// convention from <c>add-a-matcher</c> §2: an empty filter turns the
/// field off, not into a "match nothing" state.
/// </para>
/// </remarks>
public sealed class NewsMatcher : INewsMatcher
{
    private readonly SonrisaNewsDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<NewsMatcher> _logger;

    public NewsMatcher(SonrisaNewsDbContext db, IClock clock, ILogger<NewsMatcher> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate every enabled <see cref="AlertType.News"/> alert
    /// against the given <paramref name="evt"/>. Returns the set of
    /// alert ids that fired (or would have fired, in test mode). Match
    /// rows are persisted for every fire.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> RunForEventAsync(Event evt, CancellationToken ct)
    {
        if (evt.Type != AlertType.News)
        {
            // Type-aware: a News event must not trigger Market or
            // Disaster alerts. The matcher's responsibility is the
            // News half of the type matrix; market/disaster matchers
            // land in waves 7 and 8.
            return Array.Empty<Guid>();
        }

        var alerts = await _db.Alerts
            .Where(a => a.Type == AlertType.News && a.Enabled)
            .ToListAsync(ct);

        var fired = new List<Guid>();
        var firedAt = _clock.UtcNow;
        foreach (var alert in alerts)
        {
            var filters = Deserialize(alert);
            if (filters is null)
            {
                // A broken Filters column (somehow past the create-time
                // tripwire). Skip and log; don't crash the worker.
                _logger.LogWarning(
                    "Alert {AlertId} has an unparseable Filters JSON; skipping",
                    alert.Id);
                continue;
            }

            if (!Matches(filters, evt))
            {
                continue;
            }

            // Idempotency: pre-check by composite key. The unique
            // index UX_Matches_AlertId_EventId is the safety net,
            // but the in-process check keeps the log clean.
            var alreadyFired = await _db.Matches
                .AnyAsync(m => m.AlertId == alert.Id && m.EventId == evt.Id, ct);
            if (alreadyFired)
            {
                fired.Add(alert.Id);
                continue;
            }

            _db.Matches.Add(new Match
            {
                Id = Guid.NewGuid(),
                AlertId = alert.Id,
                EventId = evt.Id,
                FiredAt = firedAt,
            });
            fired.Add(alert.Id);
        }

        if (fired.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
        return fired;
    }

    /// <summary>
    /// Evaluate the matcher against the most recent
    /// <paramref name="windowSize"/> news events for the given
    /// <paramref name="alert"/>. Returns the matching events. The
    /// "Test this alert" button (wave 5 §2.2) uses this. The method
    /// does <b>not</b> insert <c>Match</c> rows — testing is a
    /// read-only preview.
    /// </summary>
    public async Task<IReadOnlyList<TestAlertHit>> RunForAlertPreviewAsync(
        Alert alert,
        int windowSize,
        CancellationToken ct)
    {
        if (alert.Type != AlertType.News)
        {
            return Array.Empty<TestAlertHit>();
        }

        var filters = Deserialize(alert);
        if (filters is null)
        {
            return Array.Empty<TestAlertHit>();
        }

        // SQLite does not support ORDER BY on DateTimeOffset; we
        // pull the window with a single FetchedAt DESC and sort in
        // memory (wave 5 handoff §2.10 — same pattern, same reason).
        var raw = await _db.Events
            .AsNoTracking()
            .Where(e => e.Type == AlertType.News)
            .Take(windowSize)
            .ToListAsync(ct);
        var recent = raw
            .OrderByDescending(e => e.FetchedAt)
            .ToList();

        var hits = new List<TestAlertHit>();
        foreach (var evt in recent)
        {
            if (!Matches(filters, evt))
            {
                continue;
            }
            hits.Add(new TestAlertHit(evt.Id, Summarize(evt)));
        }
        return hits;
    }

    /// <summary>The pure predicate. Reads the alert's filter JSON and the event's payload JSON.</summary>
    internal static bool Matches(NewsAlertFilters filters, Event evt)
    {
        // Source filter (empty = all sources)
        if (filters.SourceIds is { Count: > 0 })
        {
            if (!filters.SourceIds.Contains(evt.SourceId))
            {
                return false;
            }
        }

        // Keyword filter (null/blank = no keyword filter)
        if (!string.IsNullOrWhiteSpace(filters.Keyword))
        {
            if (!PayloadContainsKeyword(evt.Payload, filters.Keyword))
            {
                return false;
            }
        }

        // Tag filter (null/empty = no tag filter)
        if (filters.Tags is { Count: > 0 })
        {
            if (!PayloadHasTags(evt.Payload, filters.Tags, requireAll: filters.MatchMode == KeywordMatchMode.All))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PayloadContainsKeyword(string payload, string keyword)
    {
        using var doc = JsonDocument.Parse(payload);
        var haystack = ExtractText(doc.RootElement);
        if (string.IsNullOrEmpty(haystack))
        {
            return false;
        }

        // The single-keyword case is the only one we have today
        // (the filter DTO is Keyword + MatchMode; the MatchMode
        // applies to the tag list, not to a list of keywords). A
        // future wave that accepts a list of keywords would pass
        // the list and the MatchMode in here too.
        return haystack.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool PayloadHasTags(string payload, IReadOnlyList<string> tags, bool requireAll)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("tags", out var tagEl) || tagEl.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var eventTags = tagEl.EnumerateArray()
            .Select(t => t.GetString() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requireAll
            ? tags.All(t => eventTags.Contains(t))
            : tags.Any(t => eventTags.Contains(t));
    }

    private static string ExtractText(JsonElement root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var name in new[] { "title", "summary", "description" })
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var value = el.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(value);
                }
            }
        }
        return sb.ToString();
    }

    private static string Summarize(Event evt)
    {
        try
        {
            using var doc = JsonDocument.Parse(evt.Payload);
            var title = doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
            var summary = doc.RootElement.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;
            var description = doc.RootElement.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString()
                : null;
            return title ?? summary ?? description ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static NewsAlertFilters? Deserialize(Alert alert)
    {
        if (string.IsNullOrWhiteSpace(alert.Filters))
        {
            return new NewsAlertFilters(null, null, KeywordMatchMode.All, null);
        }
        try
        {
            // The alert's Filters JSON is the canonical form emitted by
            // AlertFiltersSerializer (camelCase, enums as ints). The
            // serializer's read path uses PropertyNameCaseInsensitive +
            // JsonStringEnumConverter. We mirror that here so the
            // matcher handles both wire shapes (the canonical int and
            // any legacy string form a test might seed).
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            };
            return JsonSerializer.Deserialize<NewsAlertFilters>(alert.Filters, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// One hit in the "Test this alert" preview. The Event.Id lets the
/// frontend deep-link to the source event (when the activity tab
/// lands in wave 11); the Summary is a single line of human-readable
/// text for the would-have-fired list.
/// </summary>
/// <param name="EventId">The matched <c>Event.Id</c>.</param>
/// <param name="Summary">A short human-readable line (title or summary).</param>
public sealed record TestAlertHit(Guid EventId, string Summary);
