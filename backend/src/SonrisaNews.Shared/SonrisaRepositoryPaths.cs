namespace SonrisaNews.Shared;

/// <summary>
/// Resolves the on-disk locations shared between the API host, the Worker
/// host, the EF Core design-time factory, and the dev scripts. Putting the
/// walk-up logic in one place means every entry point (whatever its CWD)
/// finds the same repo-rooted paths.
/// </summary>
/// <remarks>
/// The MVP uses a single SQLite file at <c>&lt;repo&gt;/data/sonrisa.db</c>.
/// In prod (Postgres) the path is irrelevant — the connection string comes
/// from an env var and the local path resolver is never called. The walker
/// is dev-only, but the contract (absolute path, parent dir created) is
/// stable so the wave-2 verify step works the same way for everyone.
/// </remarks>
public static class SonrisaRepositoryPaths
{
    /// <summary>
    /// Marker files that prove a directory is the repo root. The walker
    /// ascends from the supplied <paramref name="startDirectory"/> until it
    /// finds one of these. Two markers — <c>AGENTS.md</c> is the project's
    /// ground-rules file, <c>.git</c> is the VCS anchor — so a checkout that
    /// has not yet committed <c>AGENTS.md</c> still resolves.
    /// </summary>
    private static readonly string[] RepoRootMarkers = ["AGENTS.md", ".git"];

    /// <summary>
    /// Resolves the absolute path to <c>&lt;repo&gt;/data/sonrisa.db</c>,
    /// creating the <c>data/</c> directory if it does not exist. The walker
    /// is bounded to <see cref="MaxWalkDepth"/> levels so a malformed CWD
    /// (e.g. <c>C:\</c> on Windows) does not loop forever.
    /// </summary>
    /// <param name="startDirectory">
    /// Where to start the walk. Defaults to <see cref="Environment.CurrentDirectory"/>,
    /// which is the right anchor when called from the runtime host or from
    /// <c>dotnet ef</c> run from the repo root. Pass an explicit value when
    /// the caller's CWD cannot be trusted (e.g. <see cref="AppContext.BaseDirectory"/>
    /// inside the EF design-time factory, which points at the project's
    /// <c>bin/</c> folder).
    /// </param>
    public static string ResolveDevSqliteFilePath(string? startDirectory = null)
    {
        var repoRoot = FindRepoRoot(startDirectory ?? Environment.CurrentDirectory)
            ?? throw new InvalidOperationException(
                $"Could not locate the Sonrisa News repository root from '{startDirectory ?? Environment.CurrentDirectory}'. " +
                "The walker looks for AGENTS.md or .git in the current directory or any ancestor. " +
                "If you are running inside a non-standard layout, pass the repo root explicitly " +
                "as the `startDirectory` argument.");

        var dataDir = Path.Combine(repoRoot, PathConstants.DataDirectory);
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, PathConstants.SonrisaDatabaseFileName);
    }

    /// <summary>
    /// Returns the repo root for the supplied starting directory, or null if
    /// no marker is found within <see cref="MaxWalkDepth"/> levels.
    /// </summary>
    public static string? FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        for (var depth = 0; depth < MaxWalkDepth && directory is not null; depth++)
        {
            foreach (var marker in RepoRootMarkers)
            {
                if (File.Exists(Path.Combine(directory.FullName, marker)) ||
                    Directory.Exists(Path.Combine(directory.FullName, marker)))
                {
                    return directory.FullName;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private const int MaxWalkDepth = 16;
}
