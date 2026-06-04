namespace SonrisaNews.Domain.Entities;

/// <summary>
/// Append-only audit trail of admin actions (and later, system actions).
/// Drives the admin's "what happened" view and the 90-day retention rule.
/// </summary>
/// <remarks>
/// <see cref="Metadata"/> is a free-form JSON string: a change-role audit
/// stores <c>{"from": "User", "to": "Admin"}</c>, a suspend stores the reason,
/// and so on. The shape is intentionally untyped; consumers validate on read.
/// </remarks>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>The user that performed the action. Null for system-initiated entries (e.g. cleanup).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Dot-separated action name (e.g. <c>users.suspend</c>, <c>sources.enable</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The kind of entity this audit entry refers to (e.g. <c>User</c>, <c>Source</c>).</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>The id of the target entity. Stored as a string so the audit log can reference any table without an FK constraint.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Free-form JSON metadata describing the action.</summary>
    public string Metadata { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
