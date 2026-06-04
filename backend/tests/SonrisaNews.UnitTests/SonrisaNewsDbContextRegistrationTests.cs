using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Infrastructure.Persistence;
using Xunit;

namespace SonrisaNews.UnitTests;

/// <summary>
/// Wave 2 — covers the <c>AddSonrisaNewsDbContext</c> DI path. Round 3 of
/// the wave-2 implementer cycle surfaced a missing <c>using</c> on the
/// design-time factory; the runtime registration has the same shape, so the
/// two should not drift. These tests assert that the connection string from
/// <c>IConfiguration</c> reaches the resolved <c>SonrisaNewsDbContext</c>
/// unchanged. The path-resolution logic is covered separately in
/// <see cref="SonrisaRepositoryPathsTests"/>; testing it through the DI
/// surface would couple the tests to the host CWD, which is what the
/// walker exists to abstract away.
/// </summary>
[Trait("Category", DatabaseTestCategory.Database)]
public class SonrisaNewsDbContextRegistrationTests
{
    [Fact]
    public void AddSonrisaNewsDbContext_UsesAbsoluteConnectionString_FromConfiguration()
    {
        // Absolute paths pass through unchanged. The relative-path branch
        // and the blank-config fallback are covered by
        // SonrisaRepositoryPathsTests; this test pins down the
        // "absolute Data Source flows from IConfiguration to EF Core
        // verbatim" property.
        var absolutePath = Path.Combine(Path.GetTempPath(), "sonrisa-di-abs-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = $"Data Source={absolutePath}";

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sonrisa"] = connectionString,
            })
            .Build());
        services.AddSonrisaNewsDbContext();
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        using var ctx = sp.GetRequiredService<SonrisaNewsDbContext>();

        ctx.Database.GetConnectionString().Should().Be(connectionString,
            "absolute Data Source paths must reach EF Core unchanged");
    }

    [Fact]
    public void AddSonrisaNewsDbContext_IsIdempotent_AcrossMultipleInvocations()
    {
        // Calling AddSonrisaNewsDbContext twice on the same ServiceCollection
        // must not throw and must still resolve a single context. The
        // registration runs inside a closure that captures IConfiguration;
        // this test pins the "the registration is safely re-invokable"
        // property so a refactor doesn't break it. The connection string
        // also exercises the `:memory:` SQLite-special form passthrough —
        // the path walker must NOT touch these values.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sonrisa"] = "Data Source=:memory:",
            })
            .Build());
        services.AddSonrisaNewsDbContext();
        services.AddSonrisaNewsDbContext();
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        using var ctx = sp.GetRequiredService<SonrisaNewsDbContext>();

        ctx.Should().NotBeNull();
        ctx.Database.GetConnectionString().Should().Be("Data Source=:memory:",
            "SQLite-special forms like :memory: must reach EF Core unchanged; the path walker only anchors real filesystem paths");
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=file::memory:?cache=shared")]
    [InlineData("Data Source=file:sonrisa.db?mode=ro")]
    public void AddSonrisaNewsDbContext_DoesNotAnchorSqliteSpecialForms_ToRepoRoot(string connectionString)
    {
        // SQLite connection strings can carry URI-style values (`file:`)
        // and in-memory markers (`:memory:`) that are not filesystem paths.
        // The path walker must leave them alone. This is a regression guard
        // for the bug where `:memory:` was resolved to
        // `<repo-root>\:memory:` and SQLite couldn't open it.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sonrisa"] = connectionString,
            })
            .Build());
        services.AddSonrisaNewsDbContext();
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        using var ctx = sp.GetRequiredService<SonrisaNewsDbContext>();

        ctx.Database.GetConnectionString().Should().Be(connectionString);
    }
}
