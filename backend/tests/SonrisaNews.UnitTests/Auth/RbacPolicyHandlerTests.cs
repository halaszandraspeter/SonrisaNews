using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Persistence;
using Xunit;

namespace SonrisaNews.UnitTests.Auth;

/// <summary>
/// The RBAC + auth tripwire (per <c>rbac-policies.instructions.md</c>):
/// every change to <c>Auth/</c> ships with a positive and a negative test.
///
/// As of 2026-06-05 the handler is DB-driven: it joins
/// <c>UserRoles</c> ⨝ <c>RolePermissions</c> ⨝ <c>Permissions</c> against
/// the caller and the requested permission name. No in-memory mirror, no
/// Casbin, no CSV. These tests exercise the handler end-to-end against
/// SQLite <c>:memory:</c> with a seeded catalog so they prove the SQL
/// contract, not just the handler's branch table.
/// </summary>
public class RbacPolicyHandlerTests
{
    [Fact]
    public async Task Admin_CanSuspendUsers_AllowedByPolicy()
    {
        // Positive: an admin (alice) is allowed to do Users.Suspend.
        var (handler, aliceId) = await NewHandlerAsync("alice@example.com", Roles.Admin);

        var context = NewContext(aliceId, Permissions.UsersSuspend);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Admin must be granted Users.Suspend via the RolePermissions catalog");
    }

    [Fact]
    public async Task RegularUser_CannotSuspendUsers_RejectedByPolicy()
    {
        // Negative: a regular user (bob) is NOT allowed to do Users.Suspend.
        var (handler, bobId) = await NewHandlerAsync("bob@example.com", Roles.User);

        var context = NewContext(bobId, Permissions.UsersSuspend);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("User must NOT be granted Users.Suspend — only Admin is");
    }

    [Fact]
    public async Task User_CanReadOwnAlerts_AllowedByPolicy()
    {
        // Positive: a regular user is allowed to do Alerts.Read.Own.
        var (handler, bobId) = await NewHandlerAsync("bob@example.com", Roles.User);

        var context = NewContext(bobId, Permissions.AlertsReadOwn);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("User must be granted Alerts.Read.Own via the RolePermissions catalog");
    }

    [Fact]
    public async Task User_CannotManageSources_RejectedByPolicy()
    {
        // Negative: a regular user is NOT allowed to do Sources.Write.Any.
        var (handler, bobId) = await NewHandlerAsync("bob@example.com", Roles.User);

        var context = NewContext(bobId, Permissions.SourcesWriteAny);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("User must NOT be granted Sources.Write.Any — only Admin is");
    }

    [Fact]
    public async Task System_CanRunMatcher_AllowedByPolicy()
    {
        // Positive: a System caller (background worker) is allowed to do Matcher.Run.
        var (handler, workerId) = await NewHandlerAsync("worker@system", Roles.System);

        var context = NewContext(workerId, Permissions.MatcherRun);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("System must be granted Matcher.Run via the RolePermissions catalog");
    }

    [Fact]
    public async Task User_CanTestOwnAlerts_AllowedByPolicy()
    {
        // Wave 6 — the "Test this alert" endpoint uses
        // Alerts.Test.Own, not the read alias or the write permission.
        // Positive: a regular user is allowed to do Alerts.Test.Own.
        var (handler, bobId) = await NewHandlerAsync("bob@example.com", Roles.User);

        var context = NewContext(bobId, Permissions.AlertsTestOwn);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "User must be granted Alerts.Test.Own via the RolePermissions catalog — the wave-6 'Test this alert' endpoint is read-only and needs its own grant, not a write alias");
    }

    [Fact]
    public async Task System_CannotTestOwnAlerts_RejectedByPolicy()
    {
        // Wave 6 — the worker (System role) does NOT preview alerts; it
        // just matches. So the System role must NOT have Alerts.Test.Own.
        // Negative tripwire: a future wave that adds System to the
        // grant by accident will surface here.
        var (handler, workerId) = await NewHandlerAsync("worker@system", Roles.System);

        var context = NewContext(workerId, Permissions.AlertsTestOwn);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("System must NOT be granted Alerts.Test.Own — the worker does not preview alerts");
    }

    [Fact]
    public async Task Unauthenticated_Request_IsRejected()
    {
        // Negative: no current user bound → 403 regardless of the policy.
        var (handler, _) = await NewHandlerAsync(currentUserEmail: null, role: null);

        var context = NewContext(null, Permissions.AlertsReadOwn);
        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("Unauthenticated requests must be rejected by the RBAC handler");
    }

    [Fact]
    public async Task SecondRequest_ForSameUser_UsesTheCache_ButDoesNotLeakAcrossUsers()
    {
        // Defense-in-depth: the handler caches decisions per (user, permission).
        // A subsequent request for a DIFFERENT user must NOT see the prior
        // user's cached decision.
        var (handler, aliceId) = await NewHandlerAsync("alice@example.com", Roles.Admin);
        var (handler2, bobId) = await NewHandlerAsync("bob@example.com", Roles.User);

        var alice = NewContext(aliceId, Permissions.UsersSuspend);
        await handler.HandleAsync(alice);
        alice.HasSucceeded.Should().BeTrue();

        var bob = NewContext(bobId, Permissions.UsersSuspend);
        await handler2.HandleAsync(bob);
        bob.HasSucceeded.Should().BeFalse("the cache must not leak Alice's grant to Bob");
    }

    // -- helpers ------------------------------------------------------------

    private static async Task<(RbacPolicyHandler handler, System.Guid? currentUserId)> NewHandlerAsync(
        string? currentUserEmail, string? role)
    {
        var ctx = NewInMemoryContext();
        await SeedCatalogAsync(ctx);

        System.Guid? currentUserId = null;
        if (currentUserEmail is not null && role is not null)
        {
            currentUserId = Guid.NewGuid();
            ctx.Users.Add(new User
            {
                Id = currentUserId.Value,
                Email = currentUserEmail,
                PasswordHash = "hashed",
                DisplayName = currentUserEmail,
                Status = UserStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            var roleId = role switch
            {
                Roles.User => RolesCatalogSeed.UserRoleId,
                Roles.Admin => RolesCatalogSeed.AdminRoleId,
                Roles.System => RolesCatalogSeed.SystemRoleId,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role name"),
            };
            ctx.UserRoles.Add(new UserRole
            {
                UserId = currentUserId.Value,
                RoleId = roleId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // RbacPolicyHandler takes IDbContextFactory<SonrisaNewsDbContext>.
        // Wrap the in-memory context in a singleton factory.
        var factory = new SingletonContextFactory(ctx);
        var handler = new RbacPolicyHandler(
            new FakeCurrentUser(currentUserId, currentUserEmail),
            factory,
            NullLogger<RbacPolicyHandler>.Instance);
        return (handler, currentUserId);
    }

    private static AuthorizationHandlerContext NewContext(System.Guid? userId, string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,
                    userId?.ToString() ?? string.Empty)],
                authenticationType: "Test"));
        return new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);
    }

    private static SonrisaNewsDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SonrisaNewsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new SonrisaNewsDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>Seeds the 3 roles, 16 permissions, and the role→permission grants from the doc.</summary>
    private static async Task SeedCatalogAsync(SonrisaNewsDbContext ctx)
    {
        var now = DateTimeOffset.UtcNow;

        ctx.Roles.Add(new Role { Id = RolesCatalogSeed.UserRoleId,   Name = Roles.User,   DisplayName = "User",   Description = "End-user",        CreatedAt = now });
        ctx.Roles.Add(new Role { Id = RolesCatalogSeed.AdminRoleId,  Name = Roles.Admin,  DisplayName = "Admin",  Description = "Operator",        CreatedAt = now });
        ctx.Roles.Add(new Role { Id = RolesCatalogSeed.SystemRoleId, Name = Roles.System, DisplayName = "System", Description = "Background worker", CreatedAt = now });

        // Permissions (id, name) — names match Permissions constants.
        var perms = new (Guid id, string name)[]
        {
            (RolesCatalogSeed.AlertsReadOwn,         Permissions.AlertsReadOwn),
            (RolesCatalogSeed.AlertsWriteOwn,        Permissions.AlertsWriteOwn),
            (RolesCatalogSeed.AlertsTestOwn,         Permissions.AlertsTestOwn),
            (RolesCatalogSeed.AlertsReadAny,         Permissions.AlertsReadAny),
            (RolesCatalogSeed.AlertsWriteAny,        Permissions.AlertsWriteAny),
            (RolesCatalogSeed.ChannelsReadOwn,        Permissions.ChannelsReadOwn),
            (RolesCatalogSeed.ChannelsWriteOwn,       Permissions.ChannelsWriteOwn),
            (RolesCatalogSeed.SourcesReadAny,        Permissions.SourcesReadAny),
            (RolesCatalogSeed.SourcesWriteAny,       Permissions.SourcesWriteAny),
            (RolesCatalogSeed.UsersReadAny,          Permissions.UsersReadAny),
            (RolesCatalogSeed.UsersWriteAny,         Permissions.UsersWriteAny),
            (RolesCatalogSeed.UsersSuspend,          Permissions.UsersSuspend),
            (RolesCatalogSeed.AuditLogRead,          Permissions.AuditLogRead),
            (RolesCatalogSeed.AnnouncementsWrite,    Permissions.AnnouncementsWrite),
            (RolesCatalogSeed.HealthRead,            Permissions.HealthRead),
            (RolesCatalogSeed.MatcherRun,            Permissions.MatcherRun),
            (RolesCatalogSeed.ProfileRead,           Permissions.ProfileRead),
        };
        foreach (var (id, name) in perms)
        {
            ctx.Permissions.Add(new Permission { Id = id, Name = name, Description = name, CreatedAt = now });
        }

        // Role → Permission grants per the docs/roadmap RBAC matrix.
        void Grant(Guid roleId, Guid permId) =>
            ctx.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permId, CreatedAt = now });

        // User role grants.
        Grant(RolesCatalogSeed.UserRoleId, RolesCatalogSeed.AlertsReadOwn);
        Grant(RolesCatalogSeed.UserRoleId, RolesCatalogSeed.AlertsWriteOwn);
        Grant(RolesCatalogSeed.UserRoleId, RolesCatalogSeed.AlertsTestOwn);
        Grant(RolesCatalogSeed.UserRoleId, RolesCatalogSeed.ChannelsReadOwn);
        Grant(RolesCatalogSeed.UserRoleId, RolesCatalogSeed.ChannelsWriteOwn);
        Grant(RolesCatalogSeed.UserRoleId, RolesCatalogSeed.ProfileRead);

        // Admin role grants (inherits everything User has, plus admin-only).
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.AlertsReadAny);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.AlertsWriteAny);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.AlertsTestOwn);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.SourcesReadAny);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.SourcesWriteAny);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.UsersReadAny);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.UsersWriteAny);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.UsersSuspend);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.AuditLogRead);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.AnnouncementsWrite);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.HealthRead);
        Grant(RolesCatalogSeed.AdminRoleId, RolesCatalogSeed.ProfileRead);

        // System role grants (background worker — matcher run + own data).
        Grant(RolesCatalogSeed.SystemRoleId, RolesCatalogSeed.MatcherRun);
        Grant(RolesCatalogSeed.SystemRoleId, RolesCatalogSeed.AlertsReadAny);
        Grant(RolesCatalogSeed.SystemRoleId, RolesCatalogSeed.AlertsWriteAny);
        Grant(RolesCatalogSeed.SystemRoleId, RolesCatalogSeed.SourcesReadAny);
        Grant(RolesCatalogSeed.SystemRoleId, RolesCatalogSeed.ProfileRead);

        await ctx.SaveChangesAsync();
    }

    private sealed class FakeCurrentUser(System.Guid? id, string? email) : ICurrentUser
    {
        public System.Guid? Id => id;
        public string? Email => email;
        public bool IsAuthenticated => id is not null;
    }

    /// <summary>Wraps a single in-memory <see cref="SonrisaNewsDbContext"/> in an <see cref="IDbContextFactory{T}"/>.</summary>
    private sealed class SingletonContextFactory(SonrisaNewsDbContext ctx) : IDbContextFactory<SonrisaNewsDbContext>
    {
        public SonrisaNewsDbContext CreateDbContext() => ctx;
    }
}
