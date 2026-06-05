using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;
using Xunit;

namespace SonrisaNews.UnitTests.Matcher;

/// <summary>
/// Unit tests for <see cref="NewsMatcher"/>. The matcher is the
/// authoritative owner of the news-alert-firing semantics: it
/// evaluates a per-alert filter against an <see cref="Event"/> and
/// decides whether the event would have fired the alert. The
/// dispatcher (wave 8) builds on top of the rows the matcher inserts.
///
/// All tests use SQLite <c>:memory:</c> so the SQL JOIN against
/// the dedupe index (<c>UX_Matches_AlertId_EventId</c>) is exercised
/// for real — that's the wave 6 reliability rule from <c>1-features.md</c> §6
/// ("Match (alert_id, event_id) is unique; dispatcher is idempotent").
/// </summary>
[Trait("Category", WorkerIntegrationTestCategory.Matcher)]
public class NewsMatcherTests : IDisposable
{
    private readonly SonrisaNewsDbContext _db = NewDb();
    private readonly FakeClock _clock = new();
    private readonly NewsMatcher _matcher;

    public NewsMatcherTests()
    {
        _matcher = new NewsMatcher(_db, _clock, NullLogger<NewsMatcher>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // -- RunForEvent --------------------------------------------------------

    [Fact]
    public async Task RunForEvent_NewsAlertWithKeywordFilter_MatchesWhenTitleContains()
    {
        // The wave-6 verify command:
        //   Matcher_NewsAlertWithKeywordFilter_MatchesWhenTitleContains
        var alert = await SeedAlertAsync("brexit", """{"keyword":"brexit","matchMode":"All"}""");
        var evt = await SeedEventAsync(
            title: "BREXIT: Parliament passes the bill",
            summary: "Today the parliament approved the long-debated bill.",
            tags: Array.Empty<string>());

        var fired = await _matcher.RunForEventAsync(evt, CancellationToken.None);

        fired.Should().Contain(alert.Id,
            "the title contains the keyword (case-insensitive) — the alert must fire");
    }

    [Fact]
    public async Task RunForEvent_NewsAlertWithAndTags_RequiresAll()
    {
        // The wave-6 verify command:
        //   Matcher_NewsAlertWithAndKeywords_RequiresAll
        // (the doc calls them "keywords"; in the code base, the
        // semantic is on the tag list — a single keyword either is
        // present or isn't, so AND/OR only differ when there are
        // multiple values, which is the tag-list case.)
        var alert = await SeedAlertAsync(
            "ai-climate",
            """{"tags":["ai","climate"],"matchMode":"All"}""");
        var matching = await SeedEventAsync(
            title: "AI and climate: a new model",
            summary: "...",
            tags: new[] { "ai", "climate" });
        var partial = await SeedEventAsync(
            title: "AI: the latest advances",
            summary: "...",
            tags: new[] { "ai" });

        var fired = await _matcher.RunForEventAsync(matching, CancellationToken.None);
        var notFired = await _matcher.RunForEventAsync(partial, CancellationToken.None);

        fired.Should().Contain(alert.Id);
        notFired.Should().NotContain(alert.Id,
            "AND mode requires every tag to be present — only 'ai' is insufficient");
    }

    [Fact]
    public async Task RunForEvent_NewsAlertWithOrTags_RequiresAny()
    {
        // The wave-6 verify command:
        //   Matcher_NewsAlertWithOrKeywords_RequiresAny
        var alert = await SeedAlertAsync(
            "any-of-these",
            """{"tags":["ai"],"matchMode":"Any"}""");
        var matching = await SeedEventAsync(
            title: "Big news about AI today",
            summary: "...",
            tags: new[] { "ai" });
        var notMatching = await SeedEventAsync(
            title: "Stock market moves higher",
            summary: "...",
            tags: new[] { "markets" });

        var fired = await _matcher.RunForEventAsync(matching, CancellationToken.None);
        var notFired = await _matcher.RunForEventAsync(notMatching, CancellationToken.None);

        fired.Should().Contain(alert.Id);
        notFired.Should().NotContain(alert.Id,
            "OR mode requires at least one tag to be present");
    }

    [Fact]
    public async Task RunForEvent_EmptyFilter_MatchesAllNewsAlerts()
    {
        // The default-news-alert case from onboarding path C
        // (1-features.md §2.4). An empty News filter means "match
        // every news event" — the alert fires on everything.
        var alert = await SeedAlertAsync("everything", "{}");
        var evt = await SeedEventAsync(
            title: "Anything at all",
            summary: "...",
            tags: Array.Empty<string>());

        var fired = await _matcher.RunForEventAsync(evt, CancellationToken.None);

        fired.Should().Contain(alert.Id,
            "the wave-5 handoff calls this out: 'empty filter = off = match all' is the convention");
    }

    [Fact]
    public async Task RunForEvent_OnlyEnabledAlertsFire()
    {
        // A disabled alert is the master switch. The matcher respects
        // it (the source-of-truth tripwire in 1-features.md §2.4).
        var enabled = await SeedAlertAsync(
            "on", """{"keyword":"x"}""", enabled: true);
        var disabled = await SeedAlertAsync(
            "off", """{"keyword":"x"}""", enabled: false);
        var evt = await SeedEventAsync(
            title: "x marks the spot",
            summary: "...",
            tags: Array.Empty<string>());

        var fired = await _matcher.RunForEventAsync(evt, CancellationToken.None);

        fired.Should().Contain(enabled.Id);
        fired.Should().NotContain(disabled.Id,
            "the Alert.Enabled master switch must short-circuit the matcher");
    }

    [Fact]
    public async Task RunForEvent_SourceFilter_OnlyMatchingSourceFires()
    {
        // A News alert can be scoped to one or more source ids. An
        // event from a different source must not fire the alert.
        var matchingSourceId = Guid.NewGuid();
        var otherSourceId = Guid.NewGuid();
        var alert = await SeedAlertAsync(
            "scoped", $$"""{"sourceIds":["{{matchingSourceId}}"]}""");
        var matching = await SeedEventAsync(
            title: "hello",
            summary: "...",
            tags: Array.Empty<string>(),
            sourceId: matchingSourceId);
        var other = await SeedEventAsync(
            title: "hello",
            summary: "...",
            tags: Array.Empty<string>(),
            sourceId: otherSourceId);

        var matchingFired = await _matcher.RunForEventAsync(matching, CancellationToken.None);
        var otherFired = await _matcher.RunForEventAsync(other, CancellationToken.None);

        matchingFired.Should().Contain(alert.Id);
        otherFired.Should().NotContain(alert.Id,
            "a source-scoped alert must not fire for an event from a different source");
    }

    [Fact]
    public async Task RunForEvent_MarketAlertIsNotEvaluated_ForNewsEvent()
    {
        // The matcher is type-aware. A Market alert against a News
        // event must not fire (and vice versa). The (Alert.Type ==
        // Event.Type) check is the seam.
        var market = await SeedAlertAsync(
            "market", """{"symbols":["AAPL"],"percentThreshold":5.0,"windowMinutes":60}""",
            type: AlertType.Market);
        var newsEvent = await SeedEventAsync(
            title: "Apple announces new product",
            summary: "...",
            tags: Array.Empty<string>(),
            type: AlertType.News);

        var fired = await _matcher.RunForEventAsync(newsEvent, CancellationToken.None);

        fired.Should().NotContain(market.Id,
            "the matcher is type-aware — a Market alert is not evaluated against a News event");
    }

    // -- Idempotency ---------------------------------------------------------

    [Fact]
    public async Task RunForEvent_SameEventTwice_InsertsOnlyOneMatchRow()
    {
        // The wave 6 reliability rule: a flaky upstream that re-emits
        // the same item must not double-fire the alert. The unique
        // index UX_Matches_AlertId_EventId is the DB-level guard, but
        // the matcher must also short-circuit in-process to avoid a
        // constraint violation in the log on every duplicate.
        var alert = await SeedAlertAsync("once", """{"keyword":"x"}""");
        var evt = await SeedEventAsync(
            title: "x marks the spot",
            summary: "...",
            tags: Array.Empty<string>());

        var first = await _matcher.RunForEventAsync(evt, CancellationToken.None);
        var second = await _matcher.RunForEventAsync(evt, CancellationToken.None);

        first.Should().Contain(alert.Id);
        second.Should().Contain(alert.Id,
            "the matcher returns the alert id even when the match row already exists, so the dispatcher can re-read the audit trail");

        (await _db.Matches.CountAsync()).Should().Be(1,
            "a re-evaluation of the same (alert, event) pair must not insert a second match row — UX_Matches_AlertId_EventId enforces this, and the matcher short-circuits to keep the log clean");
    }

    // -- helpers ------------------------------------------------------------

    private async Task<Alert> SeedAlertAsync(string name, string filters, AlertType type = AlertType.News, bool enabled = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{name}@example.com",
            PasswordHash = "h",
            DisplayName = name,
            Status = UserStatus.Active,
        };
        _db.Users.Add(user);
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = name,
            Type = type,
            Enabled = enabled,
            Filters = filters,
        };
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();
        return alert;
    }

    private async Task<Event> SeedEventAsync(
        string title,
        string summary,
        string[] tags,
        Guid? sourceId = null,
        AlertType type = AlertType.News)
    {
        var source = new Source
        {
            Id = sourceId ?? Guid.NewGuid(),
            Type = SourceType.News,
            Name = "test-source",
            Config = "{}",
        };
        if (await _db.Sources.FindAsync(source.Id) is null)
        {
            _db.Sources.Add(source);
        }
        var payload = JsonSerializer.Serialize(new { title, summary, tags });
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            ExternalId = Guid.NewGuid().ToString("N"),
            Type = type,
            Payload = payload,
            OccurredAt = _clock.UtcNow,
            FetchedAt = _clock.UtcNow,
        };
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();
        return evt;
    }

    private static SonrisaNewsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<SonrisaNewsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new SonrisaNewsDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    }
}
