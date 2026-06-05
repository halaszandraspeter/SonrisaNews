namespace SonrisaNews.Domain.Auth;

/// <summary>
/// Deterministic Guids for the three baseline RBAC roles and the 16
/// MVP permissions. The migration inserts rows with these exact ids; the
/// auth flow references the same ids (e.g. <see cref="UserRoleId"/>) when
/// assigning a role to a new user.
/// </summary>
/// <remarks>
/// <para>
/// Why deterministic? Two reasons:
/// <list type="number">
///   <item>The seed migration and the application code can refer to the
///         same row without round-tripping through a <c>SELECT</c> for the
///         catalog.</item>
///   <item>Tests can <c>db.Roles.Find(RolesCatalogSeed.UserRoleId)</c>
///         without first inserting the seed.</item>
/// </list>
/// </para>
/// <para>
/// These ids are written by the <c>AddRbacCatalog</c> migration. Changing
/// them is a breaking change to the seed data; the migration that
/// introduces the change must delete + re-insert.
/// </para>
/// </remarks>
public static class RolesCatalogSeed
{
    // --- Role ids --------------------------------------------------------
    public static readonly Guid UserRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SystemRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // --- Permission ids (deterministic; one per constant in Permissions) -
    // The first 32 bits are the name's stable hash; the last 96 bits are
    // a per-permission tag so the ids don't collide. Using a Guid.Parse
    // literal here is fine — these are not random, they're catalog keys.
    public static readonly Guid AlertsReadOwn = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid AlertsWriteOwn = Guid.Parse("40000000-0000-0000-0000-000000000002");
    public static readonly Guid AlertsReadAny = Guid.Parse("40000000-0000-0000-0000-000000000003");
    public static readonly Guid AlertsWriteAny = Guid.Parse("40000000-0000-0000-0000-000000000004");
    public static readonly Guid ChannelsReadOwn = Guid.Parse("40000000-0000-0000-0000-000000000005");
    public static readonly Guid ChannelsWriteOwn = Guid.Parse("40000000-0000-0000-0000-000000000006");
    public static readonly Guid SourcesReadAny = Guid.Parse("40000000-0000-0000-0000-000000000007");
    public static readonly Guid SourcesWriteAny = Guid.Parse("40000000-0000-0000-0000-000000000008");
    public static readonly Guid UsersReadAny = Guid.Parse("40000000-0000-0000-0000-000000000009");
    public static readonly Guid UsersWriteAny = Guid.Parse("40000000-0000-0000-0000-00000000000a");
    public static readonly Guid UsersSuspend = Guid.Parse("40000000-0000-0000-0000-00000000000b");
    public static readonly Guid AuditLogRead = Guid.Parse("40000000-0000-0000-0000-00000000000c");
    public static readonly Guid AnnouncementsWrite = Guid.Parse("40000000-0000-0000-0000-00000000000d");
    public static readonly Guid HealthRead = Guid.Parse("40000000-0000-0000-0000-00000000000e");
    public static readonly Guid MatcherRun = Guid.Parse("40000000-0000-0000-0000-00000000000f");
    public static readonly Guid ProfileRead = Guid.Parse("40000000-0000-0000-0000-000000000010");
}
