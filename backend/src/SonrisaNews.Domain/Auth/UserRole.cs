using SonrisaNews.Domain.Entities;

namespace SonrisaNews.Domain.Auth;

/// <summary>
/// Join row: a user has a role. Composite key on
/// <c>(UserId, RoleId)</c> — a user cannot have the same role twice.
/// </summary>
/// <remarks>
/// A future wave that adds role-scoping (e.g. <c>per-team admin</c>) can
/// extend this join with a <c>Scope</c> column; the matcher in
/// <see cref="SonrisaNews.Infrastructure.Auth.RbacPolicyHandler"/> would
/// need to look at it.
/// </remarks>
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The user side of the join. Navigation only.</summary>
    public User? User { get; set; }

    /// <summary>The role side of the join. Navigation only.</summary>
    public Role? Role { get; set; }
}
