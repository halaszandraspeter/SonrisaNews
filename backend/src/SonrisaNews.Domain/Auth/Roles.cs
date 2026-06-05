namespace SonrisaNews.Domain.Auth;

/// <summary>
/// String constants for the three RBAC roles. Always use these in code rather
/// than inline strings — the rbac-audit tool and the CSV are the security
/// boundary, but the role name is referenced from multiple places and a
/// typo must not silently open up a permission.
/// </summary>
public static class Roles
{
    /// <summary>Default. Can manage own alerts, channels, profile.</summary>
    public const string User = "User";

    /// <summary>Can manage data sources, users, announcements, system health. Audit-logged on every action.</summary>
    public const string Admin = "Admin";

    /// <summary>Internal role for background workers. Cannot sign in.</summary>
    public const string System = "System";
}
