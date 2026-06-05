using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Infrastructure.Persistence;
using Xunit;

namespace SonrisaNews.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="AdminSeeder"/>. The seeder runs on every startup;
/// it is idempotent (skips if the user exists), fails fast on missing
/// config, and sets <c>MustChangePassword = true</c> so the first sign-in
/// forces a rotation.
///
/// RBAC is DB-driven: the seeder grants the <c>Admin</c> role to the
/// bootstrap user by inserting a row into <c>UserRoles</c> referencing
/// <see cref="RolesCatalogSeed.AdminRoleId"/>. The <c>User</c> entity
/// has no <c>Role</c> column any more (user rule 2026-06-05: validation
/// is by permission, not by role).
/// </summary>
[Trait("Category", AuthTestCategory.Seeder)]
public class AdminSeederTests
{
    [Fact]
    public async Task StartAsync_NoExistingUser_CreatesAnAdmin()
    {
        var db = NewDbContext();
        var options = Options(new AdminSeederOptions
        {
            Email = "admin@sonrisa.local",
            Password = "REPLACE_ME",
            DisplayName = "Bootstrap",
        });

        var seeder = new AdminSeeder(NewScopeFactory(db), options, NullLogger<AdminSeeder>.Instance);

        await seeder.StartAsync(CancellationToken.None);

        var user = await db.Users.SingleAsync();
        user.Email.Should().Be("admin@sonrisa.local");
        user.DisplayName.Should().Be("Bootstrap");
        user.Status.Should().Be(UserStatus.Active);
        user.MustChangePassword.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("REPLACE_ME", user.PasswordHash).Should().BeTrue();

        // RBAC: the user must have a row in UserRoles pointing at Admin.
        var adminGrant = await db.UserRoles
            .SingleAsync(ur => ur.UserId == user.Id);
        adminGrant.RoleId.Should().Be(RolesCatalogSeed.AdminRoleId);
    }

    [Fact]
    public async Task StartAsync_ExistingUserWithoutAdminRole_AddsAdminRole()
    {
        // Idempotency variant: an admin user from a previous version of
        // the schema (no role on the row, no UserRoles entry) must be
        // re-granted the Admin role on the next startup.
        var db = NewDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@sonrisa.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("already-set"),
            DisplayName = "Already Admin",
            Status = UserStatus.Active,
            MustChangePassword = false,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var options = Options(new AdminSeederOptions
        {
            Email = "admin@sonrisa.local",
            Password = "REPLACE_ME",
            DisplayName = "Bootstrap",
        });
        var seeder = new AdminSeeder(NewScopeFactory(db), options, NullLogger<AdminSeeder>.Instance);

        await seeder.StartAsync(CancellationToken.None);

        var users = await db.Users.ToListAsync();
        users.Should().HaveCount(1, "the seeder must not create a duplicate admin");
        users[0].DisplayName.Should().Be("Already Admin", "the existing row is not modified");

        var adminGrant = await db.UserRoles
            .SingleAsync(ur => ur.UserId == user.Id);
        adminGrant.RoleId.Should().Be(RolesCatalogSeed.AdminRoleId);
    }

    [Fact]
    public async Task StartAsync_ExistingUserWithAdminRole_DoesNotDuplicateGrant()
    {
        // The seeder is idempotent on a second startup: it must not
        // insert a second UserRoles row.
        var db = NewDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@sonrisa.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("already-set"),
            DisplayName = "Already Admin",
            Status = UserStatus.Active,
            MustChangePassword = false,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = RolesCatalogSeed.AdminRoleId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var options = Options(new AdminSeederOptions
        {
            Email = "admin@sonrisa.local",
            Password = "REPLACE_ME",
            DisplayName = "Bootstrap",
        });
        var seeder = new AdminSeeder(NewScopeFactory(db), options, NullLogger<AdminSeeder>.Instance);

        await seeder.StartAsync(CancellationToken.None);

        var grants = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync();
        grants.Should().HaveCount(1, "the seeder must not insert a second Admin grant");
    }

    [Fact]
    public async Task StartAsync_MissingEmail_Throws()
    {
        var db = NewDbContext();
        var options = Options(new AdminSeederOptions
        {
            Email = string.Empty,
            Password = "REPLACE_ME",
        });
        var seeder = new AdminSeeder(NewScopeFactory(db), options, NullLogger<AdminSeeder>.Instance);

        var act = () => seeder.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "the seeder must fail fast at startup if the email is missing — " +
            "the operator forgot to set SEED_ADMIN_EMAIL");
    }

    [Fact]
    public async Task StartAsync_MissingPassword_Throws()
    {
        var db = NewDbContext();
        var options = Options(new AdminSeederOptions
        {
            Email = "admin@sonrisa.local",
            Password = string.Empty,
        });
        var seeder = new AdminSeeder(NewScopeFactory(db), options, NullLogger<AdminSeeder>.Instance);

        var act = () => seeder.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "the seeder must fail fast at startup if the password is missing");
    }

    // -- helpers ------------------------------------------------------------

    private static SonrisaNewsDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<SonrisaNewsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new SonrisaNewsDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        SeedCatalog(ctx);
        return ctx;
    }

    /// <summary>
    /// The seeder assumes the RBAC catalog is in the DB (the
    /// <c>AddRbacCatalog</c> migration has run). In tests we don't run
    /// migrations against the in-memory DB, so we seed the catalog
    /// inline. Only the rows the seeder references are needed:
    /// <c>User</c>, <c>Admin</c>, and <c>System</c>.
    /// </summary>
    private static void SeedCatalog(SonrisaNewsDbContext ctx)
    {
        var now = DateTimeOffset.UtcNow;
        ctx.Roles.Add(new Role { Id = RolesCatalogSeed.UserRoleId,   Name = Roles.User,   DisplayName = "User",   CreatedAt = now });
        ctx.Roles.Add(new Role { Id = RolesCatalogSeed.AdminRoleId,  Name = Roles.Admin,  DisplayName = "Admin",  CreatedAt = now });
        ctx.Roles.Add(new Role { Id = RolesCatalogSeed.SystemRoleId, Name = Roles.System, DisplayName = "System", CreatedAt = now });
        ctx.SaveChanges();
    }

    private static IServiceScopeFactory NewScopeFactory(SonrisaNewsDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(ctx);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    private static Microsoft.Extensions.Options.IOptions<AdminSeederOptions> Options(AdminSeederOptions o) =>
        Microsoft.Extensions.Options.Options.Create(o);
}
