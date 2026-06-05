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
using SonrisaNews.Infrastructure.Alerts;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;
using Xunit;

namespace SonrisaNews.UnitTests.Alerts;

/// <summary>
/// Unit tests for <see cref="AlertService.TestAsync"/>. The
/// "Test this alert" button (wave 5 §2.2) re-runs the matcher against
/// the most recent 50 events for the alert and returns a "would have
/// fired" list. The test surface asserts the read-only contract:
/// <list type="bullet">
///   <item>No <c>Match</c> rows are inserted.</item>
///   <item>No <c>Notification</c> rows are inserted.</item>
///   <item>The cross-tenant case returns <c>NotFound</c> (no existence leak).</item>
/// </list>
/// </summary>
[Trait("Category", AlertsTestCategory.Service)]
public class AlertServiceTestAlertTests : IDisposable
{
    private readonly TestHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TestAsync_NewsAlertWithKeywordFilter_ReturnsMatchingRecentEvents()
    {
        // The wave-6 verify command from mvp-checklist.md §2:
        //   "Test this alert" button: re-runs the matcher with the
        //   most recent 50 events for that alert and shows what
        //   *would* have fired.
        var svc = _h.NewAlice();
        var alertResult = await svc.CreateAsync(
            new CreateAlertInput("brexit", AlertType.News, """{"keyword":"brexit","matchMode":"All"}"""),
            CancellationToken.None);

        await _h.SeedEventAsync("BREXIT: Parliament passes the bill");
        await _h.SeedEventAsync("Stock market falls");
        await _h.SeedEventAsync("BREXIT trade deal updates");

        var result = await svc.TestAsync(alertResult.Payload!.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Should().HaveCount(2,
            "the two BREXIT events would have fired — the stock-market one wouldn't");
        result.Payload!.Select(e => e.Summary).Should().AllSatisfy(s =>
            s.ToLowerInvariant().Should().Contain("brexit"));
    }

    [Fact]
    public async Task TestAsync_DoesNotInsertMatchOrNotificationRows()
    {
        // The tripwire: "Test this alert" is read-only. Re-running it
        // for the same alert must not double-fire the alert when the
        // user is satisfied and the real matcher later evaluates the
        // same events.
        var svc = _h.NewAlice();
        var alertResult = await svc.CreateAsync(
            new CreateAlertInput("x", AlertType.News, """{"keyword":"x"}"""),
            CancellationToken.None);
        await _h.SeedEventAsync("x marks the spot");
        await _h.SeedEventAsync("x is everywhere");

        var result = await svc.TestAsync(alertResult.Payload!.Id, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        (await _h.Db.Matches.CountAsync()).Should().Be(0,
            "TestAsync must not write to the Matches table — the dispatcher relies on Match rows meaning 'real fire'");
        (await _h.Db.Notifications.CountAsync()).Should().Be(0,
            "TestAsync must not write to the Notifications table either");
    }

    [Fact]
    public async Task TestAsync_NoMatchingEvents_ReturnsEmptyList()
    {
        var svc = _h.NewAlice();
        var alertResult = await svc.CreateAsync(
            new CreateAlertInput("nothing", AlertType.News, """{"keyword":"brexit"}"""),
            CancellationToken.None);
        await _h.SeedEventAsync("Stock market");
        await _h.SeedEventAsync("Weather forecast");

        var result = await svc.TestAsync(alertResult.Payload!.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Should().BeEmpty();
    }

    [Fact]
    public async Task TestAsync_AsOtherUser_ReturnsNotFound()
    {
        // Cross-tenant: bob cannot preview alice's alert. Returns
        // NotFound, not Forbidden, per the no-existence-leak rule.
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        var aliceAlert = await alice.CreateAsync(
            new CreateAlertInput("alice's", AlertType.News, """{"keyword":"x"}"""),
            CancellationToken.None);

        var result = await bob.TestAsync(aliceAlert.Payload!.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.NotFound);
    }

    [Fact]
    public async Task TestAsync_DisabledAlert_DoesNotMatch()
    {
        // The Alert.Enabled master switch applies in TestAsync too —
        // if the user has paused the alert, the "would have fired"
        // list must also be empty.
        var svc = _h.NewAlice();
        var alertResult = await svc.CreateAsync(
            new CreateAlertInput("x", AlertType.News, """{"keyword":"x"}"""),
            CancellationToken.None);
        await _h.SeedEventAsync("x marks the spot");
        await svc.UpdateAsync(
            alertResult.Payload!.Id,
            new UpdateAlertInput(Name: null, Filters: null, Enabled: false),
            CancellationToken.None);

        var result = await svc.TestAsync(alertResult.Payload!.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Should().BeEmpty(
            "a disabled alert would never have fired, so the test list is empty too");
    }

    [Fact]
    public async Task TestAsync_OnlyEvaluatesUpToFiftyMostRecentEvents()
    {
        // The wave-6 scope pin: "the most recent 50 events for that
        // alert". 60 events are seeded; the matcher sees only 50.
        // The match filter here is empty (matches everything), so the
        // 50 cap is what constrains the result size.
        var svc = _h.NewAlice();
        var alertResult = await svc.CreateAsync(
            new CreateAlertInput("everything", AlertType.News, "{}"),
            CancellationToken.None);
        for (int i = 0; i < 60; i++)
        {
            await _h.SeedEventAsync($"event #{i}");
        }

        var result = await svc.TestAsync(alertResult.Payload!.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Count.Should().BeLessThanOrEqualTo(50,
            "the doc pins the test window at the 50 most recent events");
    }

    // -- helpers ------------------------------------------------------------

    /// <summary>
    /// A shared in-memory DB. The AlertService reads + writes alerts
    /// + alert-channel-mode rows; the matcher service (injected via
    /// the service harness) reads events and writes nothing in the
    /// "test" mode. Two services built against the same DB see each
    /// other's rows.
    /// </summary>
    private sealed class TestHarness : IDisposable
    {
        public SonrisaNewsDbContext Db { get; }
        public Guid AliceId { get; }
        public Guid BobId { get; }
        public FakeClock Clock { get; } = new();

        public TestHarness()
        {
            Db = NewInMemoryContext();
            AliceId = SeedUser("alice@example.com");
            BobId = SeedUser("bob@example.com");
        }

        public AlertService NewAlice() => NewService(AliceId, "alice@example.com");
        public AlertService NewBob() => NewService(BobId, "bob@example.com");

        public async Task SeedEventAsync(string title)
        {
            var source = await Db.Sources.FirstOrDefaultAsync();
            if (source is null)
            {
                source = new Source
                {
                    Id = Guid.NewGuid(),
                    Type = SourceType.News,
                    Name = "test",
                    Config = "{}",
                };
                Db.Sources.Add(source);
                await Db.SaveChangesAsync();
            }
            var evt = new Event
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                ExternalId = Guid.NewGuid().ToString("N"),
                Type = AlertType.News,
                Payload = JsonSerializer.Serialize(new { title, summary = title }),
                OccurredAt = Clock.UtcNow,
                FetchedAt = Clock.UtcNow,
            };
            Db.Events.Add(evt);
            await Db.SaveChangesAsync();
        }

        public void Dispose() => Db.Dispose();

        private AlertService NewService(Guid? userId, string? email)
        {
            // The matcher dependency: the AlertService.TestAsync path
            // takes an INewsMatcher-shaped seam. We plug a thin
            // adapter over the test harness's NewsMatcher to keep
            // the production seam clean.
            var matcher = new NewsMatcher(Db, Clock, NullLogger<NewsMatcher>.Instance);
            return new AlertService(
                new FakeCurrentUser(userId, email),
                Db,
                Clock,
                matcher,
                NullLogger<AlertService>.Instance);
        }

        private Guid SeedUser(string email)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = "h",
                DisplayName = email.Split('@')[0],
            };
            Db.Users.Add(user);
            Db.SaveChanges();
            return user.Id;
        }

        private static SonrisaNewsDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<SonrisaNewsDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options;
            var ctx = new SonrisaNewsDbContext(options);
            ctx.Database.OpenConnection();
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(Guid? id, string? email) { Id = id; Email = email; }
        public Guid? Id { get; }
        public string? Email { get; }
        public bool IsAuthenticated => Id is not null;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    }
}
