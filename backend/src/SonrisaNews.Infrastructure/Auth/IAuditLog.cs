using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// Append-only audit trail of admin actions (and later, system actions).
/// Drives the admin "what happened" view and the 90-day retention rule.
/// </summary>
public interface IAuditLog
{
    /// <summary>Append a new audit row.</summary>
    /// <param name="actorUserId">The user that performed the action. <c>null</c> for system-initiated entries.</param>
    /// <param name="action">Dot-separated action name (e.g. <c>users.suspend</c>).</param>
    /// <param name="targetType">The kind of entity this audit entry refers to (e.g. <c>User</c>).</param>
    /// <param name="targetId">The id of the target entity, as a string (audit can reference any table without an FK).</param>
    /// <param name="metadata">Free-form JSON describing the change.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(
        Guid? actorUserId,
        string action,
        string targetType,
        string targetId,
        string metadata,
        CancellationToken ct = default);
}

public sealed class AuditLogService(IDbContextFactory<SonrisaNewsDbContext> dbContextFactory) : IAuditLog
{
    public async Task RecordAsync(
        Guid? actorUserId,
        string action,
        string targetType,
        string targetId,
        string metadata,
        CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Metadata = metadata,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
