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
using SonrisaNews.Domain.Sources;
using SonrisaNews.Infrastructure.Alerts;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Infrastructure.Sources;
using SonrisaNews.Shared;
using SonrisaNews.Worker;
using Xunit;

namespace SonrisaNews.UnitTests.WorkerIntegration;

/// <summary>
/// Wave-6 integration test: <see cref="NewsPoller"/> runs an end-to-end
/// pass against a real <see cref="SonrisaNewsDbContext"/> (SQLite
/// <c>:memory:</c>) — fetches a batch of <see cref="RawEvent"/>s from the
/// fake source, ingests them, then runs the matcher, then asserts that
/// <c>Event</c> + <c>Match</c> rows landed. This is the
/// <c>WorkerIntegration</c> slice of the wave-6 verify command.
/// </summary>
[Trait("Category", WorkerIntegrationTestCategory.Poller)]
public class NewsPollerIntegrationTests : IDisposable
{
    private readonly SonrisaNewsDbContext _db = NewDb();
    private readonly FakeClock _clock = new();
    private readonly TestHarness _h;

    public NewsPollerIntegrationTests()
    {
        _h = new TestHarness(_db, _clock);
    }

    public void Dispose()
    {
        _h.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task NewsPoller_RunsOnSchedule_PersistsEventsAndMatches()
    {
        // The wave-6 verify command:
        //   NewsPoller_RunsOnSchedule_PersistsEvents
        // (extended to assert the matcher pass too — the doc says
        // "poller + matcher" and the handoff §2.8 says the worker
        // also runs the matcher.)
        var source = await _h.SeedSourceAsync("reuters-world", new[]
        {
            new RawEvent("ext-1", AlertType.News, "{\"title\":\"BREXIT update\"}", _clock.UtcNow),
            new RawEvent("ext-2", AlertType.News, "{\"title\":\"Stock market falls\"}", _clock.UtcNow),
        });
        await _h.SeedAlertAsync(
            "brexit",
            """{"keyword":"brexit","matchMode":"All"}""",
            AlertType.News);

        var poller = _h.NewPoller();
        await poller.RunOnceAsync(CancellationToken.None);

        // 1. Events persisted (dedup via SourceId+ExternalId).
        var events = await _db.Events.Where(e => e.SourceId == source.Id).ToListAsync();
        events.Should().HaveCount(2, "both upstream items must be persisted");

        // 2. The matcher ran and inserted exactly one match — only
        // the BREXIT event matches the keyword.
        var matches = await _db.Matches.ToListAsync();
        matches.Should().HaveCount(1);
        matches[0].AlertId.Should().Be(_h.AlertId);
        var matchedEvent = await _db.Events.SingleAsync(e => e.Id == matches[0].EventId);
        matchedEvent.ExternalId.Should().Be("ext-1");

        // 3. Source.LastFetchedAt is stamped from the clock.
        var reloaded = await _db.Sources.SingleAsync();
        reloaded.LastFetchedAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task NewsPoller_FetchFails_LeavesDbUntouched_AndRecordsLastError()
    {
        // Reliability first: a transient upstream failure must NOT
        // crash the worker, and the source's LastError must surface
        // for the admin health page.
        var source = await _h.SeedSourceAsync("broken", Array.Empty<RawEvent>());
        var failingSource = new ThrowingSource(source.Id.ToString(), new InvalidOperationException("upstream is down"));
        _h.RegisterSource(failingSource);

        var poller = _h.NewPoller();
        await poller.RunOnceAsync(CancellationToken.None);

        // No events ingested.
        (await _db.Events.CountAsync()).Should().Be(0);

        // LastError recorded so the admin health page can flag this
        // source as failing.
        var reloaded = await _db.Sources.SingleAsync();
        reloaded.LastError.Should().NotBeNullOrEmpty();
        reloaded.LastError.Should().Contain("upstream is down");
    }

    [Fact]
    public async Task NewsPoller_RejectsUpstreamEvents_ForTheWrongType()
    {
        // The source says News; a defensive check stops a future
        // misconfigured source from poisoning the Events table with
        // Market rows.
        var source = await _h.SeedSourceAsync("reuters", new[]
        {
            new RawEvent("news-1", AlertType.News, "{\"title\":\"a\"}", _clock.UtcNow),
            new RawEvent("market-1", AlertType.Market, "{\"price\":1}", _clock.UtcNow),
        });

        var poller = _h.NewPoller();
        await poller.RunOnceAsync(CancellationToken.None);

        var persisted = await _db.Events.Where(e => e.SourceId == source.Id).ToListAsync();
        persisted.Should().HaveCount(1, "the Market row from a News-typed source is rejected");
        persisted[0].ExternalId.Should().Be("news-1");
    }

    // -- helpers ------------------------------------------------------------

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

    /// <summary>
    /// One in-memory DB per test class instance, shared by every
    /// service the test creates. The poller pulls together an
    /// <see cref="IHttpClientFactory"/> substitute + the source list
    /// + the ingest + the matcher so the test can swap in fakes per
    /// case.
    /// </summary>
    private sealed class TestHarness(SonrisaNewsDbContext db, IClock clock) : IDisposable
    {
        private readonly List<IDataSource> _sources = new();
        private readonly Dictionary<string, IReadOnlyList<RawEvent>> _scripted =
            new(StringComparer.Ordinal);

        public Guid AlertId { get; private set; }

        public Source CurrentSource { get; private set; } = null!;

        public async Task<Source> SeedSourceAsync(string name, IReadOnlyList<RawEvent> scripted)
        {
            var source = new Source
            {
                Id = Guid.NewGuid(),
                Type = SourceType.News,
                Name = name,
                Enabled = true,
                Config = "{}",
            };
            db.Sources.Add(source);
            await db.SaveChangesAsync();
            CurrentSource = source;
            _scripted[source.Id.ToString()] = scripted;
            _sources.Add(new ScriptedSource(source.Id.ToString(), name, scripted));
            return source;
        }

        public async Task SeedAlertAsync(string name, string filters, AlertType type)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"{name}@example.com",
                PasswordHash = "h",
                DisplayName = name,
                Status = UserStatus.Active,
            };
            db.Users.Add(user);
            var alert = new Alert
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = name,
                Type = type,
                Enabled = true,
                Filters = filters,
            };
            db.Alerts.Add(alert);
            await db.SaveChangesAsync();
            AlertId = alert.Id;
        }

        public void RegisterSource(IDataSource source) => _sources.Add(source);

        public NewsPollerRunner NewPoller() =>
            new(
                db,
                clock,
                new InMemorySourceRegistry(_sources),
                new EventIngestService(db, clock),
                new NewsMatcher(db, clock, NullLogger<NewsMatcher>.Instance),
                NullLogger<NewsPollerRunner>.Instance);

        public void Dispose() { /* nothing to dispose — db is owned by the test class */ }
    }

    /// <summary>
    /// Test double for the source registry the worker consults in
    /// production. The poller iterates <see cref="GetEnabledAsync"/>; the
    /// registry hides whether the sources were pulled from the DB or
    /// injected by a test.
    /// </summary>
    private sealed class InMemorySourceRegistry(IEnumerable<IDataSource> sources) : ISourceRegistry
    {
        public Task<IReadOnlyList<IDataSource>> GetEnabledAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IDataSource>>(sources.ToList());
    }

    private sealed class ScriptedSource(string id, string name, IReadOnlyList<RawEvent> events) : IDataSource
    {
        public string Id => id;
        public SourceType Type => SourceType.News;
        public TimeSpan PollInterval => TimeSpan.FromMinutes(2);
        public Task<IReadOnlyList<RawEvent>> FetchAsync(CancellationToken ct) => Task.FromResult(events);
    }

    private sealed class ThrowingSource(string id, Exception ex) : IDataSource
    {
        public string Id => id;
        public SourceType Type => SourceType.News;
        public TimeSpan PollInterval => TimeSpan.FromMinutes(2);
        public Task<IReadOnlyList<RawEvent>> FetchAsync(CancellationToken ct) => throw ex;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    }
}
