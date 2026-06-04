namespace SonrisaNews.Shared;

/// <summary>
/// Single source of truth for filesystem paths shared between the API, the Worker,
/// the EF Core migration tool, and the dev scripts. Putting the constant here means
/// <c>appsettings.json</c>, <c>dev.ps1</c>, and any code that resolves the DB path
/// all agree.
/// </summary>
public static class PathConstants
{
    /// <summary>Repo-relative directory that holds the SQLite database file in dev.</summary>
    public const string DataDirectory = "data";

    /// <summary>SQLite database file name (lives under <see cref="DataDirectory"/>).</summary>
    public const string SonrisaDatabaseFileName = "sonrisa.db";
}