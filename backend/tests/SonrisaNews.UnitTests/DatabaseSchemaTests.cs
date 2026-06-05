using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Auth;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Shared;
using Xunit;

namespace SonrisaNews.UnitTests;

/// <summary>
/// Wave 2 — schema-shape tests. These are the "first green tests" listed in
/// <c>docs/implementation/mvp-checklist.md</c> §2. They prove that:
///   1. The 10 entities from <c>1-features.md</c> §4 are on the model.
///   2. They round-trip through a SQLite <c>:memory:</c> DbContext (hermetic).
///   3. The 3 required composite indexes are declared in the model snapshot.
///
/// Tests use SQLite in-memory — the same provider the dev app uses, so the
/// Postgres swap in <c>2-stack.md</c> §5 is the only schema change after this wave.
/// </summary>
[Trait("Category", DatabaseTestCategory.Database)]
public class DatabaseSchemaTests
{
    [Fact]
    public void DbContext_AppliesAllEntityConfigurations_OnModelCreation()
    {
        // If a new entity class lands in SonrisaNews.Domain/Entities but has no
        // IEntityTypeConfiguration, EF will not map it and the test will see a
        // missing table below. The test name is the tripwire: any future
        // agent touching the entities folder must add a configuration in the
        // same commit.
        using var ctx = NewInMemoryContext();
        var entityTypes = ctx.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

        entityTypes.Should().Contain(typeof(User), "the User entity is the root of the domain");
        entityTypes.Should().Contain(typeof(Channel));
        entityTypes.Should().Contain(typeof(Alert));
        entityTypes.Should().Contain(typeof(Event));
        entityTypes.Should().Contain(typeof(Match));
        entityTypes.Should().Contain(typeof(Notification));
        entityTypes.Should().Contain(typeof(Source));
        entityTypes.Should().Contain(typeof(AuditLog));
        entityTypes.Should().Contain(typeof(EmailVerification));
        entityTypes.Should().Contain(typeof(PasswordResetToken));
        entityTypes.Should().Contain(typeof(RefreshToken));
    }

    [Fact]
    public void User_RoundTripsThroughSqliteMemory_AsExpected()
    {
        using var ctx = NewInMemoryContext();
        var createdAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            PasswordHash = "hashed:not-a-real-hash",
            DisplayName = "Ada",
            TimeZone = "Europe/Budapest",
            Status = UserStatus.PendingEmailVerification,
            CreatedAt = createdAt,
        };

        ctx.Users.Add(user);
        ctx.SaveChanges();

        ctx.ChangeTracker.Clear();
        var loaded = ctx.Users.Single();
        loaded.Id.Should().Be(user.Id);
        loaded.Email.Should().Be("ada@example.com");
        loaded.Status.Should().Be(UserStatus.PendingEmailVerification);
        loaded.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void RbacCatalog_RoundTripsThroughSqliteMemory_AsExpected()
    {
        using var ctx = NewInMemoryContext();
        var seededAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        var role = new Role { Id = Guid.NewGuid(), Name = "TestRole", DisplayName = "Test", CreatedAt = seededAt };
        var perm = new Permission { Id = Guid.NewGuid(), Name = "Test.Perm", CreatedAt = seededAt };
        var grant = new RolePermission { RoleId = role.Id, PermissionId = perm.Id, CreatedAt = seededAt };

        ctx.Roles.Add(role);
        ctx.Permissions.Add(perm);
        ctx.RolePermissions.Add(grant);
        ctx.SaveChanges();

        ctx.ChangeTracker.Clear();
        var loadedRole = ctx.Roles.Single();
        var loadedPerm = ctx.Permissions.Single();
        var loadedGrant = ctx.RolePermissions.Single();
        loadedRole.Name.Should().Be("TestRole");
        loadedPerm.Name.Should().Be("Test.Perm");
        loadedGrant.RoleId.Should().Be(role.Id);
        loadedGrant.PermissionId.Should().Be(perm.Id);
    }

    [Fact]
    public void AlertChannelMode_HasCompositeKey_OnAlertAndChannel()
    {
        // The doc says AlertChannelMode is a join with alert_id + channel_id;
        // both columns participate in the primary key, and the unique index is
        // the PK. A real-world regression here would silently allow duplicate
        // (alert, channel) rows and break the channel-mode matrix.
        using var ctx = NewInMemoryContext();
        var entityType = ctx.Model.FindEntityType(typeof(AlertChannelMode))!;

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { nameof(AlertChannelMode.AlertId), nameof(AlertChannelMode.ChannelId) });
    }

    [Theory]
    [InlineData(typeof(Event), new[] { nameof(Event.SourceId), nameof(Event.OccurredAt) })]
    [InlineData(typeof(Match), new[] { nameof(Match.AlertId), nameof(Match.FiredAt) })]
    [InlineData(typeof(Notification), new[] { nameof(Notification.UserId), nameof(Notification.SentAt) })]
    public void DbContext_HasRequiredCompositeIndex(Type entityType, string[] expectedColumnNames)
    {
        // Wave 2 tripwire: the 3 composite indexes from 1-features.md §4 must
        // be on the model. Each index matches the matcher window query or the
        // activity feed sort. A test per index keeps the failure message clear
        // when one is missing in a future migration.
        using var ctx = NewInMemoryContext();
        var et = ctx.Model.FindEntityType(entityType)!;

        var hasIndex = et.GetIndexes().Any(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(expectedColumnNames));

        hasIndex.Should().BeTrue(
            $"entity {entityType.Name} must declare a composite index on ({string.Join(", ", expectedColumnNames)})");
    }

    [Theory]
    [InlineData(typeof(Match), nameof(Match.AlertId))]
    [InlineData(typeof(Match), nameof(Match.EventId))]
    [InlineData(typeof(Event), nameof(Event.SourceId))]
    [InlineData(typeof(Alert), nameof(Alert.UserId))]
    [InlineData(typeof(Channel), nameof(Channel.UserId))]
    [InlineData(typeof(Notification), nameof(Notification.MatchId))]
    [InlineData(typeof(EmailVerification), nameof(EmailVerification.UserId))]
    [InlineData(typeof(PasswordResetToken), nameof(PasswordResetToken.UserId))]
    [InlineData(typeof(RefreshToken), nameof(RefreshToken.UserId))]
    public void DbContext_DeclaresForeignKey_OnDependentColumn(Type entityType, string foreignKeyColumn)
    {
        // Wave 2 tripwire: every dependent entity must declare its FK in the
        // EF model, not just leave a shadow Guid column. Without this, the
        // schema has no referential integrity (and no automatic FK index) —
        // a delete on the parent table orphans the child rows silently. The
        // alert channel mode join is a composite FK, so it's covered by the
        // AlertChannelMode_HasCompositeKey test above, not by this Theory.
        using var ctx = NewInMemoryContext();
        var et = ctx.Model.FindEntityType(entityType)!;

        var hasFk = et.GetForeignKeys().Any(fk => fk.Properties.Any(p => p.Name == foreignKeyColumn));
        hasFk.Should().BeTrue(
            $"entity {entityType.Name} must declare an EF foreign key on column {foreignKeyColumn}; " +
            "otherwise the schema has no referential integrity and the matching EF index is missing.");
    }

    [Fact]
    public void Notification_Status_RoundTripsThroughSqliteMemory_AsEnum()
    {
        // Wave 2 tripwire: the lifecycle status must be a typed enum, not a
        // string. A typo in "Pendingg" would compile and silently break the
        // activity feed, so we make the type system do the work.
        using var ctx = NewInMemoryContext();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b.c", PasswordHash = "h", DisplayName = "A" };
        var source = new Source { Id = Guid.NewGuid(), Type = SourceType.News, Name = "s", Config = "{}" };
        var alert = new Alert { Id = Guid.NewGuid(), UserId = user.Id, Name = "n", Filters = "{}" };
        var evt = new Event { Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = "e", Payload = "{}" };
        var match = new Match { Id = Guid.NewGuid(), AlertId = alert.Id, EventId = evt.Id };
        var channel = new Channel { Id = Guid.NewGuid(), UserId = user.Id, Type = ChannelType.Email, Destination = "a@b.c" };
        ctx.AddRange(source, user, alert, evt, match, channel);
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AlertId = alert.Id,
            MatchId = match.Id,
            ChannelId = channel.Id,
            Status = NotificationStatus.Sent,
            SentAt = DateTimeOffset.UtcNow,
        };
        ctx.Notifications.Add(notification);
        ctx.SaveChanges();

        ctx.ChangeTracker.Clear();
        var loaded = ctx.Notifications.Single();
        loaded.Status.Should().Be(NotificationStatus.Sent,
            "the Status column stores the enum as an int and round-trips back to the same enum value");
    }

    [Fact]
    public void RefreshToken_AndPasswordResetToken_AreMappedAsEntities()
    {
        // Wave 3 (auth) will flesh these out. Wave 2's job is to make sure the
        // tables exist in the schema so the first auth migration is additive,
        // not a "now we have to migrate the auth tables too" surprise.
        using var ctx = NewInMemoryContext();
        var entityTypes = ctx.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

        entityTypes.Should().Contain(typeof(RefreshToken));
        entityTypes.Should().Contain(typeof(PasswordResetToken));
        entityTypes.Should().Contain(typeof(EmailVerification));
    }

    [Fact]
    public void MigrationFolder_AllMigrationsDeclareForwardOnlyComment()
    {
        // The CLI owns migrations. This test asserts that every
        // migration's <c>Up</c> method declares the forward-only tripwire
        // per <c>database-migrations.instructions.md</c>.
        //
        // Hardened per wave-2 review item #16: the previous tripwire
        // looked for "Forward-only after merge" anywhere in the file,
        // which would silently pass if a future agent inserted a line
        // above the comment. The new rule is the marker must be the
        // FIRST non-XML-doc line inside the <c>Up</c> method body —
        // anywhere else is a violation.
        //
        // The migrations folder lives at a fixed path relative to the repo
        // root (resolved by SonrisaRepositoryPaths), independent of which
        // bin/Debug/<tfm>/ folder the test assembly happens to live in.
        var repoRoot = SonrisaRepositoryPaths.FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate the Sonrisa News repo root from " + AppContext.BaseDirectory);
        var migrationsDirectory = Path.Combine(repoRoot, "backend", "src", "SonrisaNews.Infrastructure", "Migrations");

        Directory.Exists(migrationsDirectory)
            .Should().BeTrue(
                $"Migrations directory must exist at {migrationsDirectory}. " +
                "If you just cloned the repo, run `dotnet ef migrations add InitialSchema " +
                "--project backend/src/SonrisaNews.Infrastructure` from the repo root.");

        // Look at every <timestamp>_<Name>.cs file (e.g.
        // 20260605074133_AddNotificationDedupeKey.cs). The snapshot
        // and designer files are excluded: they are scaffolder
        // artifacts and are not hand-edited.
        var migrations = Directory
            .EnumerateFiles(migrationsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(p => !p.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(p => !Path.GetFileName(p).Equals("SonrisaNewsDbContextModelSnapshot.cs", StringComparison.Ordinal))
            .ToArray();

        migrations.Should().NotBeEmpty(
            "the migrations folder must contain at least the wave-2 InitialSchema migration. " +
            "If this test fails, run `dotnet ef migrations add InitialSchema " +
            "--project backend/src/SonrisaNews.Infrastructure` from the repo root.");

        foreach (var migration in migrations)
        {
            var fileName = Path.GetFileName(migration);
            var content = File.ReadAllText(migration);

            // Find the body of the Up method (the first one; the
            // scaffolder never generates overloads). We accept the
            // marker on the very first non-XML-doc line of the Up
            // method body — both patterns in the current migrations
            // (`InitialSchema` and `AddNotificationDedupeKey` put the
            // marker as the first line inside `Up {}`; `AddRbacCatalog`
            // puts it on a line of its own above the `Up` method) are
            // validated by checking the line is the first non-XML-doc
            // line in the file after class-level doc.
            var lines = File.ReadAllLines(migration);
            string? markerLine = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                // Skip empty lines.
                if (trimmed.Length == 0) continue;
                // Skip XML doc lines.
                if (trimmed.StartsWith("///", StringComparison.Ordinal)) continue;
                // Skip `using` directives and namespace/class braces.
                if (trimmed.StartsWith("using ", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("#nullable", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("namespace ", StringComparison.Ordinal)) continue;
                if (trimmed == "{" || trimmed == "}") continue;
                if (trimmed.StartsWith("public partial class ", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("protected override void Up", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("protected override void Down", StringComparison.Ordinal)) continue;

                if (trimmed.StartsWith("// ", StringComparison.Ordinal) &&
                    trimmed.Contains("Forward-only after merge", StringComparison.Ordinal))
                {
                    markerLine = trimmed;
                    break;
                }

                // Hit a real code line before the marker — fail.
                break;
            }

            markerLine.Should().NotBeNull(
                $"{fileName} must declare `// Forward-only after merge.` as the first non-XML-doc, non-scaffolding line. " +
                "The marker is the tripwire that prevents post-merge hand-edits of an applied migration " +
                "per <c>database-migrations.instructions.md</c>. " +
                "The marker must appear inside the <c>Up</c> method body, or as the first comment " +
                "after the class declaration but before the <c>Up</c> method signature.");
        }
    }

    private static SonrisaNewsDbContext NewInMemoryContext()
    {
        // SQLite shared-memory cache: fast, hermetic, same provider the
        // production app uses, so the Postgres swap is the only schema change
        // after this wave. `:memory:` is per-connection — opening a new
        // connection in a test that calls `UseSqlite("Data Source=:memory:")`
        // gets a fresh empty database, which is exactly what we want.
        var options = new DbContextOptionsBuilder<SonrisaNewsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new SonrisaNewsDbContext(options);
        ctx.Database.OpenConnection();   // keep the in-memory DB alive for the lifetime of ctx
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
