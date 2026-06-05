namespace SonrisaNews.Domain.Auth;

/// <summary>
/// A role in the RBAC catalog. Roles are seeded at migration time; the
/// three MVP rows are <c>User</c>, <c>Admin</c>, and <c>System</c>. A user
/// has zero or more roles via the <c>UserRoles</c> join table; a role has
/// zero or more permissions via the <c>RolePermissions</c> join table.
/// </summary>
/// <remarks>
/// The <see cref="Name"/> column is the stable, code-time identifier (e.g.
/// <c>"Admin"</c>). The <see cref="DisplayName"/> is for the admin UI.
/// </remarks>
public class Role
{
    public Guid Id { get; set; }

    /// <summary>Stable code-time identifier. The <see cref="Roles"/> constants are the only values a code path may compare against.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable name for the admin UI.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Free-form description; shown in the admin "roles" page.</summary>
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The user→role grants for this role. Navigation only — the DB is the source of truth.</summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];

    /// <summary>The role→permission grants for this role. Navigation only.</summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
