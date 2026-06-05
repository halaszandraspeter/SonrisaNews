using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Infrastructure.Matcher;

/// <summary>
/// The seam between the alert service and the news matcher. The
/// matcher is the only place that knows how to evaluate a
/// <c>NewsAlertFilters</c> against an <c>Event</c>; the alert service
/// (which serves the "Test this alert" endpoint) depends on this
/// interface, not the concrete <see cref="NewsMatcher"/>, so the
/// test double is trivial to substitute.
/// </summary>
public interface INewsMatcher
{
    /// <summary>
    /// Evaluate the matcher against the most recent
    /// <paramref name="windowSize"/> news events for the given
    /// <paramref name="alert"/>. Returns the matching events. The
    /// method does NOT insert <c>Match</c> rows — this is the
    /// "Test this alert" read-only preview.
    /// </summary>
    Task<IReadOnlyList<TestAlertHit>> RunForAlertPreviewAsync(
        Alert alert,
        int windowSize,
        CancellationToken ct);
}
