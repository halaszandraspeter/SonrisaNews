using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Auth;

/// <summary>
/// DB-driven authorization handler. Resolves
/// <see cref="PermissionRequirement"/> with a single SQL JOIN against
/// <c>UserRoles</c> + <c>RolePermissions</c> + <c>Permissions</c> filtered by
/// the current user's id and the permission name. The result is cached
/// per (userId, permission) within the lifetime of the handler (i.e.
/// the singleton lifetime; one cache entry per request in the common case).
/// </summary>
/// <remarks>
/// <para>
/// <b>DB is the source of truth.</b> No Casbin, no CSV, no in-memory
/// enforcer. The RbacAudit tool checks every
/// <c>[Authorize(Policy = Permissions.X)]</c> in code against the
/// <c>Permissions</c> + <c>RolePermissions</c> tables.
/// </para>
/// <para>
/// <b>Validation is by permission, never by role.</b> The handler
/// resolves the user's permissions via the join; there is no
/// <c>if (user.Role == Admin)</c> short-circuit anywhere in this
/// codebase. The <see cref="ICurrentUser"/> abstraction therefore
/// does not expose the user's role — only the user id, which is enough
/// for the join.
/// </para>
/// <para>
/// <b>Resource ownership</b> (e.g. "this alert belongs to the caller")
/// is enforced at the service layer, not here. This handler answers
/// the coarse question "may a user with this role perform this
/// action on the resource class at all?"
/// </para>
/// <para>
/// <b>System role.</b> The background worker (waves 6–8) binds an
/// <see cref="ICurrentUser"/> with the user's email or id set to a
/// well-known <c>System</c> principal; the join then resolves
/// <c>Matcher.Run</c> from the <c>System</c> role's grants.
/// </para>
/// </remarks>
public sealed class RbacPolicyHandler(
    ICurrentUser currentUser,
    IDbContextFactory<SonrisaNewsDbContext> dbContextFactory,
    ILogger<RbacPolicyHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// Per-instance cache. Lifetime is <c>Singleton</c> (see
    /// <see cref="AuthServiceCollectionExtensions.AddSonrisaNewsAuth"/>), so
    /// the cache persists across requests and is shared between threads.
    /// The value is <c>bool</c>; the dictionary's absence is "not yet
    /// checked" and the present value is "checked, allowed = true|false".
    /// </summary>
    private readonly ConcurrentDictionary<CacheKey, bool> _cache = new();

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (currentUser.Id is null)
        {
            // No identity bound — the JwtBearer middleware should have
            // already returned 401, so reaching here is a misconfiguration
            // (a controller marked [Authorize] without the [AllowAnonymous]
            // shortcut). Fail closed.
            logger.LogWarning(
                "RBAC handler saw no current user for permission {Permission}; denying.",
                requirement.Permission);
            return;
        }

        var key = new CacheKey(currentUser.Id.Value, requirement.Permission);
        if (_cache.TryGetValue(key, out var cached))
        {
            if (cached) context.Succeed(requirement);
            return;
        }

        // One DB hit per (user, permission) per handler lifetime. The
        // handler is a singleton, so the cache is process-wide; that's
        // safe because permissions only change on rare admin actions
        // and the next process restart (e.g. a deploy) rehydrates the
        // cache. If we ever need invalidation, the right move is a
        // background reloader (not per-request DB hits).
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var allowed = await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == currentUser.Id.Value)
            .Join(db.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionId)
            .Join(db.Permissions,
                permissionId => permissionId,
                p => p.Id,
                (permissionId, p) => p.Name)
            .AnyAsync(name => name == requirement.Permission);

        _cache[key] = allowed;
        if (allowed) context.Succeed(requirement);
        else
        {
            // Information, not Warning: a 403 on a permission a user
            // doesn't have is the *common* path of the API, not an
            // anomaly. Logged at Information so a brute-force scan
            // doesn't fill the warning channel; the audit log row
            // (logged separately by the controller) is the
            // security-grade record of who tried what.
            logger.LogInformation(
                "RBAC denied for user {UserId} on {Permission}",
                currentUser.Id,
                requirement.Permission);
        }
    }

    /// <summary>Cache key = (user, permission). The pair uniquely identifies one authorization decision.</summary>
    private readonly record struct CacheKey(Guid UserId, string Permission);
}
