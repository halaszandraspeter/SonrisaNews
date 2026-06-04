using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Persistence;

/// <summary>DI helpers for the EF Core context. Call from the API and Worker <c>Program.cs</c>.</summary>
public static class SonrisaNewsDbContextRegistration
{
    /// <summary>
    /// Registers the DbContext against SQLite. The connection string is read from
    /// <see cref="ConfigurationKeys.SonrisaDatabase"/> under <see cref="ConfigurationKeys.ConnectionStrings"/>.
    /// </summary>
    /// <remarks>
    /// Resolution order (highest priority first): environment variable
    /// (<c>ConnectionStrings__Sonrisa</c>), <c>dotnet user-secrets</c>, then
    /// <c>appsettings.{Environment}.json</c>. A relative <c>Data Source=</c>
    /// path is resolved against the repo root (the directory containing
    /// <c>AGENTS.md</c>), so a process started from any CWD ends up at the
    /// same SQLite file. If no connection string is configured, the dev
    /// default at <c>&lt;repo&gt;/data/sonrisa.db</c> is used and the
    /// <c>data/</c> directory is created on demand.
    /// </remarks>
    public static IServiceCollection AddSonrisaNewsDbContext(this IServiceCollection services)
    {
        services.AddDbContext<SonrisaNewsDbContext>((sp, options) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(ConfigurationKeys.SonrisaDatabase);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // No override — fall back to the dev default at the repo
                // root. This is the same path the design-time factory uses,
                // so `dotnet ef` and the runtime host land on the same file.
                connectionString = $"Data Source={SonrisaRepositoryPaths.ResolveDevSqliteFilePath(Environment.CurrentDirectory)}";
            }
            else if (StartsWithDataSource(connectionString)
                     && IsRelativeFilesystemPath(ExtractDataSource(connectionString)))
            {
                // The configured path is relative. Anchor it to the repo
                // root (or the process CWD if the repo root can't be found)
                // so the same appsettings value resolves to the same file
                // regardless of which directory the host was launched from.
                // SQLite-special forms (`:memory:`, `file::memory:?cache=shared`,
                // `:memory:?cache=shared`) bypass the path walker — they're
                // not filesystem paths.
                var repoRoot = SonrisaRepositoryPaths.FindRepoRoot(Environment.CurrentDirectory)
                    ?? Environment.CurrentDirectory;
                connectionString = $"Data Source={Path.Combine(repoRoot, ExtractDataSource(connectionString)).Replace('/', Path.DirectorySeparatorChar)}";
            }

            options.UseSqlite(connectionString);
        });

        return services;
    }

    private static bool StartsWithDataSource(string connectionString) =>
        connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

    private static string ExtractDataSource(string connectionString) =>
        connectionString["Data Source=".Length..].Trim();

    /// <summary>
    /// True if the value is a relative filesystem path that the repo-root
    /// walker should anchor. False for absolute paths (already resolved)
    /// and for SQLite-special forms (`:memory:`, `file::memory:?cache=shared`,
    /// etc.) which are not filesystem locations.
    /// </summary>
    private static bool IsRelativeFilesystemPath(string dataSource)
    {
        if (Path.IsPathRooted(dataSource))
        {
            return false;
        }

        // SQLite's in-memory form: starts with a colon, or starts with
        // "file:" (URI-style connection strings).
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