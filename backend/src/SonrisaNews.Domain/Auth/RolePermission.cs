namespace SonrisaNews.Domain.Auth;

/// <summary>
/// Join row: a role has a permission. Composite key on
/// <c>(RoleId, PermissionId)</c> — a role cannot have the same permission twice.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The role side of the join. Navigation only.</summary>
    public Role? Role { get; set; }

    /// <summary>The permission side of the join. Navigation only.</summary>
    public Permission? Permission { get; set; }
}
