using SonrisaNews.Domain;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Matcher;

namespace SonrisaNews.Infrastructure.Alerts;

/// <summary>
/// Business-logic seam for alert CRUD + channel-mode matrix. The
/// controller is a thin HTTP adapter; this service is the authoritative
/// owner of the alert-ownership invariant, the filter-validation
/// tripwire, and the channel-mode join.
/// </summary>
public interface IAlertService
{
    /// <summary>List the caller's alerts, newest first. Returns empty for a new user.</summary>
    Task<AlertResult<IReadOnlyList<Alert>>> ListAsync(CancellationToken ct);

    /// <summary>Get a single alert by id. Returns <see cref="AlertOutcome.NotFound"/> if the alert does not exist OR does not belong to the caller.</summary>
    Task<AlertResult<Alert>> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Create a new alert owned by the caller. Validates the filter JSON shape per <see cref="AlertType"/>.</summary>
    Task<AlertResult<Alert>> CreateAsync(CreateAlertInput input, CancellationToken ct);

    /// <summary>Update mutable fields on an existing alert owned by the caller. Validates the filter JSON if present.</summary>
    Task<AlertResult<Alert>> UpdateAsync(Guid id, UpdateAlertInput input, CancellationToken ct);

    /// <summary>Delete an alert owned by the caller. Cascade removes its channel-mode rows.</summary>
    Task<AlertResult<bool>> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>List the channel-mode rows for an alert owned by the caller.</summary>
    Task<AlertResult<IReadOnlyList<AlertChannelMode>>> ListChannelModesAsync(Guid alertId, CancellationToken ct);

    /// <summary>Upsert a channel-mode row for an alert owned by the caller. The channel must also be owned by the caller.</summary>
    Task<AlertResult<AlertChannelMode>> SetChannelModeAsync(Guid alertId, SetChannelModeInput input, CancellationToken ct);

    /// <summary>Remove a channel-mode row for an alert owned by the caller.</summary>
    Task<AlertResult<bool>> RemoveChannelModeAsync(Guid alertId, Guid channelId, CancellationToken ct);

    /// <summary>
    /// Re-run the matcher against the most recent 50 events for the
    /// given alert and return the "would have fired" list. The
    /// method does <b>not</b> insert <c>Match</c> or
    /// <c>Notification</c> rows — this is a read-only preview for
    /// the "Test this alert" button (wave 5 §2.2 / wave 6 scope).
    /// </summary>
    Task<AlertResult<IReadOnlyList<TestAlertHit>>> TestAsync(Guid alertId, CancellationToken ct);
}
