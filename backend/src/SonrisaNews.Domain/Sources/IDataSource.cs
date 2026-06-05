using System.Threading;
using System.Threading.Tasks;

namespace SonrisaNews.Domain.Sources;

/// <summary>
/// A pluggable upstream data source — an RSS feed, a USGS earthquake
/// feed, a yfinance market symbol group. The worker iterates the
/// registered <see cref="IDataSource"/> instances on a per-source
/// cadence; the admin can also trigger an out-of-band fetch via the
/// admin UI (wave 10).
/// </summary>
/// <remarks>
/// <para>
/// One <see cref="IDataSource"/> per upstream. The interface is
/// intentionally <b>not</b> generic over the event shape — the
/// upstream's payload lands in <see cref="RawEvent.Payload"/> as JSON
/// and the matcher reads it via <c>JsonDocument</c>. The poller doesn't
/// care about the shape; the matcher does.
/// </para>
/// <para>
/// The "registered in DI" contract is on the implementer. The
/// <c>add-a-data-source</c> skill registers each source with
/// <c>services.AddSingleton&lt;IDataSource, TSource&gt;()</c>.
/// </para>
/// </remarks>
public interface IDataSource
{
    /// <summary>
    /// Stable, code-time identifier. Matches the <c>Source.Id</c> row
    /// the admin seeds in the DB. Once an id is in the DB, it must
    /// not change — renaming creates a new source in the user's mind
    /// but orphans the events tied to the old id.
    /// </summary>
    string Id { get; }

    /// <summary>The category of upstream. Drives which matchers listen for the events.</summary>
    Domain.SourceType Type { get; }

    /// <summary>
    /// The desired poll cadence. The poller scheduler is the authority
    /// on the actual cadence; the value is a hint, not a contract.
    /// </summary>
    System.TimeSpan PollInterval { get; }

    /// <summary>
    /// Fetch a single batch of events from the upstream. Returns
    /// <c>IReadOnlyList&lt;RawEvent&gt;</c> on success. <b>Must not
    /// throw</b> on a transient upstream failure — return an empty list
    /// and log. The poller's per-source loop relies on this for
    /// reliability; a throw kills the worker.
    /// </summary>
    /// <param name="ct">Cancellation token; the worker passes the
    /// loop's stopping token so shutdown is responsive.</param>
    Task<IReadOnlyList<RawEvent>> FetchAsync(CancellationToken ct);
}
