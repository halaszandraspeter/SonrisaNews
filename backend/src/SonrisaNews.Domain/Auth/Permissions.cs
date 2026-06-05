namespace SonrisaNews.Domain.Auth;

/// <summary>
/// String constants for every RBAC permission in the system. These are the
/// names used in <c>[Authorize(Policy = Permissions.X)]</c> attributes and
/// must match the <c>Permissions</c> table rows in the DB (seeded by the
/// <c>AddRbacCatalog</c> migration).
/// </summary>
/// <remarks>
/// Format: <c>&lt;Resource&gt;.&lt;Action&gt;.&lt;Scope&gt;</c>. The
/// <c>RbacPolicyHandler</c> looks up each name against the
/// <c>UserRoles ⨝ RolePermissions ⨝ Permissions</c> join. The rbac-audit
/// tool treats these names as the source of truth for what code uses.
/// </remarks>
public static class Permissions
{
    public const string AlertsReadOwn = "Alerts.Read.Own";
    public const string AlertsWriteOwn = "Alerts.Write.Own";
    public const string AlertsReadAny = "Alerts.Read.Any";
    public const string AlertsWriteAny = "Alerts.Write.Any";

    public const string ChannelsReadOwn = "Channels.Read.Own";
    public const string ChannelsWriteOwn = "Channels.Write.Own";

    public const string SourcesReadAny = "Sources.Read.Any";
    public const string SourcesWriteAny = "Sources.Write.Any";

    public const string UsersReadAny = "Users.Read.Any";
    public const string UsersWriteAny = "Users.Write.Any";
    public const string UsersSuspend = "Users.Suspend";

    public const string AuditLogRead = "AuditLog.Read";
    public const string AnnouncementsWrite = "Announcements.Write";
    public const string HealthRead = "Health.Read";

    /// <summary>Background worker only. Not granted to a user-facing JWT.</summary>
    public const string MatcherRun = "Matcher.Run";

    /// <summary>Read-only view of the current user. Granted to every authenticated user.</summary>
    public const string ProfileRead = "Profile.Read";
}
