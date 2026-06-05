using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Domain.Sources;
using SonrisaNews.Infrastructure.Sources;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;
using Xunit;

namespace SonrisaNews.UnitTests.Sources;

/// <summary>
/// Unit tests for <see cref="EventIngestService"/>. The service is the
/// reliability tripwire for the "no duplicate events" rule from
/// <c>1-features.md</c> §6: the poller feeds a batch of <see cref="RawEvent"/>s
/// into the service, and the service persists only the ones that don't
/// already exist (matched on <c>(SourceId, ExternalId)</c>).
/// </summary>
[Trait("Category", WorkerIntegrationTestCategory.Sources)]
public class EventIngestServiceTests : IDisposable
{
    private readonly SonrisaNewsDbContext _db = NewDb();
    private readonly FakeClock _clock = new();
    private readonly EventIngestService _service;

    public EventIngestServiceTests()
    {
        _service = new EventIngestService(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task IngestAsync_NewEvents_PersistsAllAndReturnsIds()
    {
        var source = await SeedSourceAsync("s1");
        var batch = new[]
        {
            NewRaw("ext-1", "First event"),
            NewRaw("ext-2", "Second event"),
        };

        var ids = await _service.IngestAsync(source.Id, AlertType.News, batch, CancellationToken.None);

        ids.Should().HaveCount(2);
        var events = await _db.Events.Where(e => e.SourceId == source.Id).ToListAsync();
        events.Should().HaveCount(2);
        events.Select(e => e.ExternalId).Should().BeEquivalentTo(new[] { "ext-1", "ext-2" });
    }

    [Fact]
    public async Task IngestAsync_DuplicateExternalId_IsIdempotent()
    {
        // The wave-6 verify command:
        //   RssSource_DuplicateExternalId_IsIdempotent
        // (translated: a flaky RSS that re-emits the same item must
        // not create duplicate events.)
        var source = await SeedSourceAsync("s1");
        var first = await _service.IngestAsync(
            source.Id, AlertType.News,
            new[] { NewRaw("ext-1", "First event") },
            CancellationToken.None);
        first.Should().HaveCount(1);

        // Second pass: same source, same external id, different body
        // (the upstream republished the same item with a different
        // content hash). The service must NOT insert a second row.
        var second = await _service.IngestAsync(
            source.Id, AlertType.News,
            new[] { NewRaw("ext-1", "First event, reposted") },
            CancellationToken.None);
        second.Should().BeEmpty(
            "the second pass returns the list of newly-inserted ids — duplicates are skipped");

        (await _db.Events.CountAsync(e => e.SourceId == source.Id)).Should().Be(1,
            "the duplicate must be silently skipped, not surfaced as an error");
    }

    [Fact]
    public async Task IngestAsync_MixedNewAndDuplicate_InsertsOnlyNewOnes()
    {
        var source = await SeedSourceAsync("s1");
        await _service.IngestAsync(
            source.Id, AlertType.News,
            new[] { NewRaw("ext-1", "First") },
            CancellationToken.None);

        var batch = new[]
        {
            NewRaw("ext-1", "First, reposted"),    // dup
            NewRaw("ext-2", "Second"),            // new
            NewRaw("ext-3", "Third"),             // new
        };
        var ids = await _service.IngestAsync(source.Id, AlertType.News, batch, CancellationToken.None);

        ids.Should().HaveCount(2,
            "only the new external ids are inserted; the dup returns an empty set in the new-ids list");
        (await _db.Events.CountAsync(e => e.SourceId == source.Id)).Should().Be(3);
    }

    [Fact]
    public async Task IngestAsync_StampsFetchedAt_FromClock()
    {
        var source = await SeedSourceAsync("s1");
        await _service.IngestAsync(
            source.Id, AlertType.News,
            new[] { NewRaw("ext-1", "First") },
            CancellationToken.None);

        var evt = await _db.Events.SingleAsync();
        evt.FetchedAt.Should().Be(_clock.UtcNow,
            "the service stamps FetchedAt from IClock so the 30-day retention cutoff in the cleanup service is reproducible");
    }

    [Fact]
    public async Task IngestAsync_PreservesPayload_AsJson()
    {
        var source = await SeedSourceAsync("s1");
        var payload = "{\"title\":\"hello\",\"summary\":\"world\"}";
        await _service.IngestAsync(
            source.Id, AlertType.News,
            new[] { new RawEvent("ext-1", AlertType.News, payload, _clock.UtcNow) },
            CancellationToken.None);

        var evt = await _db.Events.SingleAsync();
        evt.Payload.Should().Be(payload,
            "the raw upstream payload is stored verbatim so the matcher can read whatever fields it needs");
    }

    // -- helpers ------------------------------------------------------------

    private static RawEvent NewRaw(string externalId, string body) =>
        new(externalId, AlertType.News, $"{{\"title\":\"{body}\",\"summary\":\"{body}\"}}", DateTimeOffset.UtcNow);

    private async Task<Source> SeedSourceAsync(string name)
    {
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Type = SourceType.News,
            Name = name,
            Config = "{}",
        };
        _db.Sources.Add(source);
        await _db.SaveChangesAsync();
        return source;
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
