using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="SonrisaNewsDbContext"/>. Used by
/// <c>dotnet ef</c> at design time (migrations add, migrations script,
/// database update) when there is no running host to read DI from.
/// </summary>
/// <remarks>
/// <para>
/// The factory resolves the same <c>ConnectionStrings:Sonrisa</c> config
/// key the runtime host uses, so a dev's local <c>appsettings.Development.json</c>
/// or environment variable Just Works for <c>dotnet ef</c> as well.
/// </para>
/// <para>
/// Why this exists: <c>dotnet ef</c> needs to build a <see cref="SonrisaNewsDbContext"/>
/// at design time, but the runtime host's <c>Program.cs</c> is not in the
/// Infrastructure project — it's in the API project. Without this factory,
/// the EF tools either try to spin up the API host (and fail) or fail to
/// resolve the DbContext options entirely.
/// </para>
/// </remarks>
public class SonrisaNewsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SonrisaNewsDbContext>
{
    public SonrisaNewsDbContext CreateDbContext(string[] args)
    {
        // The EF tools invoke this from the directory the operator ran the
        // command from (which may be the repo root, the Infrastructure dir,
        // or anywhere else). The dev connection string points at
        // data/sonrisa.db, but SQLite interprets that path relative to the
        // process CWD — so the simplest robust thing is to resolve an
        // absolute path via SonrisaRepositoryPaths, which walks up to the
        // repo root and creates data/ if needed.
        var devConnectionString = $"Data Source={SonrisaRepositoryPaths.ResolveDevSqliteFilePath(AppContext.BaseDirectory)}";

        // The runtime host also reads appsettings; mirror its resolution
        // order so an env var override (ConnectionStrings__Sonrisa) still
        // wins over the dev default. This is the same playbook the runtime
        // emits on the missing-connection-string error path.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Sonrisa");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fall back to the absolute dev path so a fresh clone that has
            // never been bootstrapped can still run `dotnet ef migrations add`.
            connectionString = devConnectionString;
        }
        else if (IsRelativeSqliteDataSource(connectionString))
        {
            // The appsettings value is a relative filesystem path. Resolve
            // it the same way the runtime host does (via the repo root),
            // so a `dotnet ef ...` from any CWD ends up at the same file.
            // SQLite-special forms (`:memory:`, `file::memory:?cache=shared`,
            // URI-style connection strings) bypass the path walker.
            var repoRoot = SonrisaRepositoryPaths.FindRepoRoot(AppContext.BaseDirectory)
                ?? SonrisaRepositoryPaths.FindRepoRoot(Environment.CurrentDirectory)
                ?? Environment.CurrentDirectory;
            connectionString = $"Data Source={Path.Combine(
                repoRoot,
                connectionString["Data Source=".Length..]).Replace('/', Path.DirectorySeparatorChar)}";
        }

        var options = new DbContextOptionsBuilder<SonrisaNewsDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new SonrisaNewsDbContext(options);
    }

    private static bool IsRelativeSqliteDataSource(string connectionString)
    {
        if (!connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dataSource = connectionString["Data Source=".Length..].Trim();

        // Already absolute — leave it alone.
        if (Path.IsPathRooted(dataSource))
        {
            return false;
        }

        // SQLite in-memory and URI-style forms. These are not filesystem paths.
        if (dataSource.StartsWith(":", StringComparison.Ordinal))
        {
            return false;
        }
        if (dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
