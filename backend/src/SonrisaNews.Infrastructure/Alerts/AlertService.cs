using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Alerts;

/// <summary>
/// Default <see cref="IAlertService"/> implementation. Owns the
/// alert-ownership invariant and the per-type filter validation tripwire.
/// </summary>
/// <remarks>
/// The service uses a scoped <see cref="SonrisaNewsDbContext"/> (same
/// pattern as <c>ChannelsController</c>) — every method opens a scope
/// for the duration of the call. The auth flow uses an
/// <c>IDbContextFactory</c> because the seeder hosted service
/// intersects with the API's request pipeline; the alert service has no
/// such concern.
/// </remarks>
public sealed class AlertService(
    ICurrentUser currentUser,
    SonrisaNewsDbContext db,
    IClock clock,
    ILogger<AlertService> logger) : IAlertService
{
    /// <summary>Max number of alerts returned in a single list call. Pagination is post-MVP.</summary>
    private const int ListPageSize = 200;

    public async Task<AlertResult<IReadOnlyList<Alert>>> ListAsync(CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<IReadOnlyList<Alert>>.Failure(AlertOutcome.Unauthenticated);
        }

        var alerts = await db.Alerts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

        // SQLite does not support ORDER BY on DateTimeOffset; the
        // post-MVP Postgres swap will keep this in LINQ-to-Objects for
        // the same reason (the alert count per user is small in MVP).
        var ordered = alerts
            .OrderByDescending(a => a.CreatedAt)
            .Take(ListPageSize)
            .ToList();

        return AlertResult<IReadOnlyList<Alert>>.Success(ordered);
    }

    public async Task<AlertResult<Alert>> GetAsync(Guid id, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.Unauthenticated);
        }

        var alert = await db.Alerts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        return alert is null
            ? AlertResult<Alert>.Failure(AlertOutcome.NotFound)
            : AlertResult<Alert>.Success(alert);
    }

    public async Task<AlertResult<Alert>> CreateAsync(CreateAlertInput input, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.Unauthenticated);
        }

        var trimmedName = (input.Name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.InvalidFilter,
                new[] { new AlertFiltersError("name", "Name is required.") });
        }

        if (trimmedName.Length > 200)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.InvalidFilter,
                new[] { new AlertFiltersError("name", "Name must be 200 characters or fewer.") });
        }

        var filterResult = AlertFiltersSerializer.TryDeserialize(input.Type, input.Filters ?? "{}");
        if (!filterResult.Success)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.InvalidFilter, filterResult.Errors);
        }

        var now = clock.UtcNow;
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            Type = input.Type,
            Enabled = true,
            Filters = filterResult.Value!,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Alert {AlertId} created for user {UserId} (type {AlertType})", alert.Id, userId, alert.Type);

        return AlertResult<Alert>.Success(alert);
    }

    public async Task<AlertResult<Alert>> UpdateAsync(Guid id, UpdateAlertInput input, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.Unauthenticated);
        }

        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (alert is null)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.NotFound);
        }

        var fieldErrors = new List<AlertFiltersError>();
        var hasChanges = false;

        if (input.Name is not null)
        {
            var trimmed = input.Name.Trim();
            if (trimmed.Length == 0)
            {
                fieldErrors.Add(new("name", "Name is required."));
            }
            else if (trimmed.Length > 200)
            {
                fieldErrors.Add(new("name", "Name must be 200 characters or fewer."));
            }
            else if (trimmed != alert.Name)
            {
                alert.Name = trimmed;
                hasChanges = true;
            }
        }

        if (input.Filters is not null)
        {
            var filterResult = AlertFiltersSerializer.TryDeserialize(alert.Type, input.Filters);
            if (!filterResult.Success)
            {
                fieldErrors.AddRange(filterResult.Errors);
            }
            else if (filterResult.Value != alert.Filters)
            {
                alert.Filters = filterResult.Value!;
                hasChanges = true;
            }
        }

        if (input.Enabled is not null && input.Enabled.Value != alert.Enabled)
        {
            alert.Enabled = input.Enabled.Value;
            hasChanges = true;
        }

        if (fieldErrors.Count > 0)
        {
            return AlertResult<Alert>.Failure(AlertOutcome.InvalidFilter, fieldErrors);
        }

        if (hasChanges)
        {
            alert.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return AlertResult<Alert>.Success(alert);
    }

    public async Task<AlertResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<bool>.Failure(AlertOutcome.Unauthenticated);
        }

        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (alert is null)
        {
            return AlertResult<bool>.Failure(AlertOutcome.NotFound);
        }

        db.Alerts.Remove(alert);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Alert {AlertId} deleted for user {UserId}", id, userId);

        return AlertResult<bool>.Success(true);
    }

    public async Task<AlertResult<IReadOnlyList<AlertChannelMode>>> ListChannelModesAsync(Guid alertId, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<IReadOnlyList<AlertChannelMode>>.Failure(AlertOutcome.Unauthenticated);
        }

        var alertExists = await db.Alerts
            .AsNoTracking()
            .AnyAsync(a => a.Id == alertId && a.UserId == userId, ct);
        if (!alertExists)
        {
            return AlertResult<IReadOnlyList<AlertChannelMode>>.Failure(AlertOutcome.NotFound);
        }

        var rows = await db.AlertChannelModes
            .AsNoTracking()
            .Where(acm => acm.AlertId == alertId)
            .ToListAsync(ct);

        return AlertResult<IReadOnlyList<AlertChannelMode>>.Success(rows);
    }

    public async Task<AlertResult<AlertChannelMode>> SetChannelModeAsync(Guid alertId, SetChannelModeInput input, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<AlertChannelMode>.Failure(AlertOutcome.Unauthenticated);
        }

        // The alert ownership check runs first; the channel ownership
        // check runs only if the alert is owned. Both probe directions
        // return NotFound (not ChannelNotFound) so a user with their
        // own channel cannot probe other users' alert ids by trying
        // random ones, AND a user with no channel cannot probe other
        // users' alert ids either — both probes are uniformly NotFound.
        var alertTask = db.Alerts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId, ct);
        var channelTask = db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ChannelId && c.UserId == userId, ct);
        await Task.WhenAll(alertTask, channelTask);

        if (alertTask.Result is null)
        {
            return AlertResult<AlertChannelMode>.Failure(AlertOutcome.NotFound);
        }

        if (channelTask.Result is null)
        {
            return AlertResult<AlertChannelMode>.Failure(AlertOutcome.ChannelNotFound);
        }

        var row = await db.AlertChannelModes
            .FirstOrDefaultAsync(acm => acm.AlertId == alertId && acm.ChannelId == input.ChannelId, ct);

        if (row is null)
        {
            row = new AlertChannelMode
            {
                AlertId = alertId,
                ChannelId = input.ChannelId,
                Mode = input.Mode,
                CreatedAt = clock.UtcNow,
            };
            db.AlertChannelModes.Add(row);
        }
        else if (row.Mode != input.Mode)
        {
            row.Mode = input.Mode;
        }

        await db.SaveChangesAsync(ct);

        return AlertResult<AlertChannelMode>.Success(row);
    }

    public async Task<AlertResult<bool>> RemoveChannelModeAsync(Guid alertId, Guid channelId, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId)
        {
            return AlertResult<bool>.Failure(AlertOutcome.Unauthenticated);
        }

        // Confirm the alert belongs to the caller before touching the
        // join row. Otherwise a user could probe which (alert, channel)
        // pairs exist by trying random ids.
        var alertOwned = await db.Alerts
            .AsNoTracking()
            .AnyAsync(a => a.Id == alertId && a.UserId == userId, ct);
        if (!alertOwned)
        {
            return AlertResult<bool>.Failure(AlertOutcome.NotFound);
        }

        var row = await db.AlertChannelModes
            .FirstOrDefaultAsync(acm => acm.AlertId == alertId && acm.ChannelId == channelId, ct);

        if (row is null)
        {
            return AlertResult<bool>.Failure(AlertOutcome.NotFound);
        }

        db.AlertChannelModes.Remove(row);
        await db.SaveChangesAsync(ct);

        return AlertResult<bool>.Success(true);
    }
}
