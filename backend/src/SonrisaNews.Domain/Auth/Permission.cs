namespace SonrisaNews.Domain.Auth;

/// <summary>
/// A permission in the RBAC catalog. Permissions are seeded at migration
/// time; one row per constant in <see cref="Permissions"/>. A permission
/// has zero or more roles via the <c>RolePermissions</c> join table.
/// </summary>
/// <remarks>
/// The <see cref="Name"/> column is the stable, code-time identifier (e.g.
/// <c>"Alerts.Read.Own"</c>). Every <c>[Authorize(Policy = Permissions.X)]</c>
/// in a controller must have a row in this table AND a grant in
/// <c>RolePermissions</c> for at least one role; the <c>rbac-audit</c>
/// tool enforces this.
/// </remarks>
public class Permission
{
    public Guid Id { get; set; }

    /// <summary>Stable code-time identifier. Matches the <see cref="Permissions"/> constants exactly.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable description; shown in the admin "permissions" page.</summary>
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The role→permission grants for this permission. Navigation only.</summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
