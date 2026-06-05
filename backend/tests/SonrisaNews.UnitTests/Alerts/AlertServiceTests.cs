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
/// Unit tests for <see cref="AlertService"/>. The service is the
/// authoritative owner of the alert-ownership invariant and the
/// per-type filter validation tripwire called out in
/// <c>docs/implementation/mvp-checklist.md</c> §2 (wave 5).
/// </summary>
[Trait("Category", AlertsTestCategory.Service)]
public class AlertServiceTests : IDisposable
{
    private readonly TestHarness _h = new();

    public void Dispose() => _h.Dispose();

    // -- Create ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_NewsWithKeywordFilter_PersistsFilter()
    {
        // The verify command in mvp-checklist §2 (wave 5):
        // "CreateAlert_NewsWithKeywordFilter_PersistsFilter"
        var svc = _h.NewAlice();
        var input = NewCreateInput(
            name: "Brexit news",
            type: AlertType.News,
            filters: """{"keyword":"brexit","matchMode":"All"}""");

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Filters.Should().Contain("\"keyword\":\"brexit\"",
            "the canonical Filters JSON must echo the parsed keyword back to the DB");
    }

    [Fact]
    public async Task CreateAsync_InvalidFilter_ReturnsFailure()
    {
        // The verify command: "CreateAlert_InvalidFilter_Returns400"
        // (the controller maps the typed InvalidFilter outcome to 400).
        var svc = _h.NewAlice();
        var input = NewCreateInput(
            name: "Bad alert",
            type: AlertType.Market,
            filters: """{"keyword":"not a market field"}""");  // "keyword" is not a Market field

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.InvalidFilter);
        result.Errors.Should().NotBeNull();
        result.Errors!.Should().Contain(e => e.Field == "keyword");
    }

    [Fact]
    public async Task CreateAsync_BlankName_ReturnsFailure()
    {
        var svc = _h.NewAlice();
        var input = NewCreateInput(name: "   ", type: AlertType.News, filters: "{}");

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.InvalidFilter);
        result.Errors.Should().NotBeNull();
        result.Errors!.Should().Contain(e => e.Field == "name");
    }

    [Fact]
    public async Task CreateAsync_AlertIsOwnedByTheCaller()
    {
        var aliceId = _h.AliceId;
        var svc = _h.NewAlice();
        var input = NewCreateInput(name: "Owned", type: AlertType.News, filters: "{}");

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.UserId.Should().Be(aliceId,
            "the alert must be persisted with the caller's id, not a request-supplied id");
    }

    [Fact]
    public async Task CreateAsync_MarketFilter_PersistsThresholdAndWindow()
    {
        var svc = _h.NewAlice();
        var input = NewCreateInput(
            name: "Big movers",
            type: AlertType.Market,
            filters: """{"symbols":["AAPL","MSFT"],"percentThreshold":3.5,"windowMinutes":30}""");

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var doc = System.Text.Json.JsonDocument.Parse(result.Payload!.Filters);
        var root = doc.RootElement;
        root.GetProperty("percentThreshold").GetDouble().Should().Be(3.5);
        root.GetProperty("windowMinutes").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task CreateAsync_DisasterFilter_RejectsUnknownField()
    {
        var svc = _h.NewAlice();
        var input = NewCreateInput(
            name: "Earthquakes",
            type: AlertType.Disaster,
            filters: """{"regions":["JP"],"eventTypes":["earthquake"],"minSeverity":5.0,"daysBack":7}""");

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.InvalidFilter);
        result.Errors!.Should().Contain(e => e.Field == "daysBack");
    }

    [Fact]
    public async Task CreateAsync_Unauthenticated_ReturnsFailure()
    {
        var svc = _h.NewUnauthenticated();
        var input = NewCreateInput(name: "x", type: AlertType.News, filters: "{}");

        var result = await svc.CreateAsync(input, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.Unauthenticated);
    }

    // -- List & get -----------------------------------------------------------

    [Fact]
    public async Task ListAsync_ReturnsOnlyOwnedAlerts()
    {
        // Two owners: alice and bob. Each creates one alert. The
        // listing for alice must return only alice's alerts.
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        await alice.CreateAsync(NewCreateInput("a1", AlertType.News, "{}"), CancellationToken.None);
        await alice.CreateAsync(NewCreateInput("a2", AlertType.News, "{}"), CancellationToken.None);
        await bob.CreateAsync(NewCreateInput("b1", AlertType.News, "{}"), CancellationToken.None);

        var aliceList = await alice.ListAsync(CancellationToken.None);
        var bobList = await bob.ListAsync(CancellationToken.None);

        aliceList.IsSuccess.Should().BeTrue();
        aliceList.Payload.Should().HaveCount(2);
        aliceList.Payload!.Select(a => a.Name).Should().BeEquivalentTo(new[] { "a1", "a2" });
        aliceList.Payload.Should().OnlyContain(a => a.UserId == _h.AliceId);

        bobList.IsSuccess.Should().BeTrue();
        bobList.Payload.Should().ContainSingle();
        bobList.Payload!.Single().UserId.Should().Be(_h.BobId);
    }

    [Fact]
    public async Task GetAsync_AsOtherUser_ReturnsNotFound()
    {
        // The verify command's spirit: "UpdateAlert_AsOtherUser_Returns403"
        // — we map the cross-tenant lookup to 404 (no existence leak),
        // not 403, per openapi-schema.instructions.md.
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        var created = await alice.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await bob.GetAsync(created.Payload!.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.NotFound);
    }

    // -- Update ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_OwnedAlert_ChangesName()
    {
        var svc = _h.NewAlice();
        var created = await svc.CreateAsync(NewCreateInput("old", AlertType.News, "{}"), CancellationToken.None);

        var result = await svc.UpdateAsync(
            created.Payload!.Id,
            new UpdateAlertInput(Name: "new", Filters: null, Enabled: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Name.Should().Be("new");
    }

    [Fact]
    public async Task UpdateAsync_OwnedAlert_ChangesFilters()
    {
        var svc = _h.NewAlice();
        var created = await svc.CreateAsync(
            NewCreateInput("a", AlertType.News, """{"keyword":"old"}"""),
            CancellationToken.None);

        var result = await svc.UpdateAsync(
            created.Payload!.Id,
            new UpdateAlertInput(Name: null, Filters: """{"keyword":"new","matchMode":"Any"}""", Enabled: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Filters.Should().Contain("\"new\"");
    }

    [Fact]
    public async Task UpdateAsync_OwnedAlert_ChangesEnabled()
    {
        var svc = _h.NewAlice();
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await svc.UpdateAsync(
            created.Payload!.Id,
            new UpdateAlertInput(Name: null, Filters: null, Enabled: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_AsOtherUser_ReturnsNotFound()
    {
        // The verify command: "UpdateAlert_AsOtherUser_Returns403" — we
        // surface 404 (not 403) to avoid existence leaks.
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        var created = await alice.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await bob.UpdateAsync(
            created.Payload!.Id,
            new UpdateAlertInput(Name: "hijack", Filters: null, Enabled: null),
            CancellationToken.None);

        result.Outcome.Should().Be(AlertOutcome.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_InvalidFilter_ReturnsFailure()
    {
        var svc = _h.NewAlice();
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.Market, """{"symbols":["AAPL"],"percentThreshold":5.0,"windowMinutes":60}"""), CancellationToken.None);

        var result = await svc.UpdateAsync(
            created.Payload!.Id,
            new UpdateAlertInput(Name: null, Filters: """{"symbols":[],"percentThreshold":5.0,"windowMinutes":60}""", Enabled: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(AlertOutcome.InvalidFilter);
    }

    // -- Delete ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_OwnedAlert_RemovesIt()
    {
        var svc = _h.NewAlice();
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var delete = await svc.DeleteAsync(created.Payload!.Id, CancellationToken.None);
        delete.IsSuccess.Should().BeTrue();

        (await _h.Db.Alerts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_AsOtherUser_ReturnsNotFound()
    {
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        var created = await alice.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await bob.DeleteAsync(created.Payload!.Id, CancellationToken.None);
        result.Outcome.Should().Be(AlertOutcome.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAlertChannelModeRows()
    {
        // The join is Cascade on Alert delete, so channel-mode rows
        // must disappear too.
        var svc = _h.NewAlice();
        var channel = await _h.SeedChannelFor(_h.AliceId);
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);
        await svc.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(channel.Id, DeliveryMode.DigestDaily),
            CancellationToken.None);

        await svc.DeleteAsync(created.Payload!.Id, CancellationToken.None);

        (await _h.Db.AlertChannelModes.CountAsync()).Should().Be(0);
    }

    // -- Channel-mode matrix --------------------------------------------------

    [Fact]
    public async Task SetChannelMode_NewChannel_AddsAlertChannelMode()
    {
        var svc = _h.NewAlice();
        var channel = await _h.SeedChannelFor(_h.AliceId);
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await svc.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(channel.Id, DeliveryMode.Digest15m),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Mode.Should().Be(DeliveryMode.Digest15m);

        var row = await _h.Db.AlertChannelModes.SingleAsync();
        row.AlertId.Should().Be(created.Payload.Id);
        row.ChannelId.Should().Be(channel.Id);
        row.Mode.Should().Be(DeliveryMode.Digest15m);
    }

    [Fact]
    public async Task SetChannelMode_ExistingChannel_UpdatesMode()
    {
        var svc = _h.NewAlice();
        var channel = await _h.SeedChannelFor(_h.AliceId);
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);
        await svc.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(channel.Id, DeliveryMode.Realtime),
            CancellationToken.None);

        var result = await svc.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(channel.Id, DeliveryMode.DigestDaily),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Payload!.Mode.Should().Be(DeliveryMode.DigestDaily);
        (await _h.Db.AlertChannelModes.CountAsync()).Should().Be(1,
            "the second call must update, not insert a second row");
    }

    [Fact]
    public async Task SetChannelMode_OtherUsersChannel_ReturnsChannelNotFound()
    {
        // Cross-tenant: alert owned by alice, channel owned by bob.
        var alice = _h.NewAlice();
        var bobChannel = await _h.SeedChannelFor(_h.BobId);
        var created = await alice.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await alice.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(bobChannel.Id, DeliveryMode.Realtime),
            CancellationToken.None);

        result.Outcome.Should().Be(AlertOutcome.ChannelNotFound,
            "an alert owner cannot wire to a channel they do not own");
    }

    [Fact]
    public async Task SetChannelMode_AlertOwnedByAlice_AsBob_ReturnsNotFound()
    {
        // Cross-tenant symmetric case: alice has the alert, bob has
        // the channel. Bob tries to wire HIS channel to ALICE'S alert.
        // The service must report the alert as not-found (not the
        // channel) — otherwise bob can probe which of alice's alert
        // ids exist by trying random ids with a channel he owns.
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        var bobChannel = await _h.SeedChannelFor(_h.BobId);
        var created = await alice.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await bob.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(bobChannel.Id, DeliveryMode.Realtime),
            CancellationToken.None);

        result.Outcome.Should().Be(AlertOutcome.NotFound,
            "the alert ownership check runs first — a future refactor that swaps the order would let a user with their own channel probe other users' alert ids by trying random ones");

        (await _h.Db.AlertChannelModes.CountAsync()).Should().Be(0,
            "no channel-mode row must be written when the caller does not own the alert");
    }

    [Fact]
    public async Task SetChannelMode_OtherUsersAlert_ReturnsNotFound()
    {
        var bob = _h.NewBob();
        var bobChannel = await _h.SeedChannelFor(_h.BobId);

        var result = await bob.SetChannelModeAsync(
            Guid.NewGuid(),  // not bob's alert id
            new SetChannelModeInput(bobChannel.Id, DeliveryMode.Realtime),
            CancellationToken.None);

        result.Outcome.Should().Be(AlertOutcome.NotFound);
    }

    [Fact]
    public async Task RemoveChannelMode_Existing_RemovesRow()
    {
        var svc = _h.NewAlice();
        var channel = await _h.SeedChannelFor(_h.AliceId);
        var created = await svc.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);
        await svc.SetChannelModeAsync(
            created.Payload!.Id,
            new SetChannelModeInput(channel.Id, DeliveryMode.Realtime),
            CancellationToken.None);

        var result = await svc.RemoveChannelModeAsync(created.Payload!.Id, channel.Id, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        (await _h.Db.AlertChannelModes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ListChannelModes_AsOtherUser_ReturnsNotFound()
    {
        var alice = _h.NewAlice();
        var bob = _h.NewBob();
        var created = await alice.CreateAsync(NewCreateInput("a", AlertType.News, "{}"), CancellationToken.None);

        var result = await bob.ListChannelModesAsync(created.Payload!.Id, CancellationToken.None);
        result.Outcome.Should().Be(AlertOutcome.NotFound);
    }

    // -- helpers --------------------------------------------------------------

    private static CreateAlertInput NewCreateInput(string name, AlertType type, string filters) =>
        new(Name: name, Type: type, Filters: filters);

    /// <summary>
    /// One in-memory DB per test class instance, shared by every
    /// <c>AlertService</c> the test creates. Two services built against
    /// the same DB see each other's rows — which is the only way
    /// cross-tenant tests work.
    /// </summary>
    private sealed class TestHarness : IDisposable
    {
        public SonrisaNewsDbContext Db { get; }

        public Guid AliceId { get; }
        public Guid BobId { get; }

        public TestHarness()
        {
            Db = NewInMemoryContext();
            AliceId = SeedUser("alice@example.com");
            BobId = SeedUser("bob@example.com");
        }

        public AlertService NewAlice() => NewService(AliceId, "alice@example.com");
        public AlertService NewBob() => NewService(BobId, "bob@example.com");
        public AlertService NewUnauthenticated() => NewService(null, null);

        public Task<Channel> SeedChannelFor(Guid userId) => SeedChannelAsync(Db, userId);

        public void Dispose() => Db.Dispose();

        private AlertService NewService(Guid? userId, string? email) =>
            new(new FakeCurrentUser(userId, email),
                Db,
                new FakeClock(),
                new NewsMatcher(Db, new FakeClock(), NullLogger<NewsMatcher>.Instance),
                NullLogger<AlertService>.Instance);

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

        private static async Task<Channel> SeedChannelAsync(SonrisaNewsDbContext db, Guid userId)
        {
            var channel = new Channel
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = ChannelType.Email,
                Destination = $"{userId}@example.com",
            };
            db.Channels.Add(channel);
            await db.SaveChangesAsync();
            return channel;
        }
    }

    /// <summary>Minimal <see cref="ICurrentUser"/> for tests. The AlertService only reads <c>Id</c>.</summary>
    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(Guid? id, string? email)
        {
            Id = id;
            Email = email;
        }

        public Guid? Id { get; }
        public string? Email { get; }
        public bool IsAuthenticated => Id is not null;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    }
}
