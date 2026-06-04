using FluentAssertions;
using SonrisaNews.Shared;
using Xunit;

namespace SonrisaNews.UnitTests;

/// <summary>
/// Tests for the repo-root walker. The walker is the seam between "where the
/// process happens to be" and "where the SQLite file lives in dev", and
/// every host (API, Worker, EF tool) goes through it. If this drifts, the
/// EF tool's "unable to open database file" error returns.
/// </summary>
[Trait("Category", DatabaseTestCategory.Database)]
public class SonrisaRepositoryPathsTests : IDisposable
{
    private readonly string _scratchRoot;

    public SonrisaRepositoryPathsTests()
    {
        // Each test runs in a synthetic temp dir shaped like a tiny repo.
        // The walker needs an AGENTS.md (or .git) marker to identify the
        // root; we use AGENTS.md to mirror the real Sonrisa News layout.
        _scratchRoot = Path.Combine(Path.GetTempPath(), "sonrisa-paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratchRoot);
        File.WriteAllText(Path.Combine(_scratchRoot, "AGENTS.md"), "# scratch");
    }

    public void Dispose()
    {
        // Best-effort cleanup. If a test crashed mid-write, the temp dir
        // will leak; the GUID suffix keeps the next test from colliding.
        try { Directory.Delete(_scratchRoot, recursive: true); } catch { /* best-effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FindRepoRoot_WalksUpFromNestedDir_FindsTheMarker()
    {
        // Nested 3 levels deep — proves the walker actually ascends.
        var nested = Path.Combine(_scratchRoot, "a", "b", "c");
        Directory.CreateDirectory(nested);

        var root = SonrisaRepositoryPaths.FindRepoRoot(nested);

        root.Should().Be(_scratchRoot);
    }

    [Fact]
    public void FindRepoRoot_FindsTheClosestAncestorWithAMarker()
    {
        // The walker returns the CLOSEST ancestor with a marker — the one it
        // hits first as it ascends. That's the right behavior for nested
        // git worktrees or vendored sub-repos: the inner AGENTS.md wins over
        // the outer one. We verify by putting another AGENTS.md one level
        // deeper than the scratch root and asserting the walker returns the
        // inner one, not the outer.
        var inner = Path.Combine(_scratchRoot, "inner");
        Directory.CreateDirectory(inner);
        File.WriteAllText(Path.Combine(inner, "AGENTS.md"), "# inner scratch — should be the answer");

        var deeper = Path.Combine(inner, "a", "b");
        Directory.CreateDirectory(deeper);

        var root = SonrisaRepositoryPaths.FindRepoRoot(deeper);

        root.Should().Be(inner,
            "the walker returns the first marker it hits while ascending, which is the inner AGENTS.md");
    }

    [Fact]
    public void ResolveDevSqliteFilePath_CreatesTheDataDirectory()
    {
        // The whole point of the resolver is to make `dotnet ef database
        // update` work on a fresh clone, where data/ doesn't exist yet.
        // Asserting Directory.Exists before and after is the tripwire that
        // would have caught the original "unable to open database file"
        // error in the wave-2 verify.
        var nested = Path.Combine(_scratchRoot, "deep");
        Directory.CreateDirectory(nested);

        var dataDir = Path.Combine(_scratchRoot, PathConstants.DataDirectory);
        Directory.Exists(dataDir).Should().BeFalse("the scratch repo starts with no data/ dir — proves the resolver creates it");

        var resolved = SonrisaRepositoryPaths.ResolveDevSqliteFilePath(nested);

        Directory.Exists(dataDir).Should().BeTrue("the resolver must create the data/ dir on demand");
        File.Exists(resolved).Should().BeFalse("the resolver should NOT pre-create the .db file; SQLite creates it on first connection");
        resolved.Should().Be(Path.Combine(dataDir, PathConstants.SonrisaDatabaseFileName));
    }
}
