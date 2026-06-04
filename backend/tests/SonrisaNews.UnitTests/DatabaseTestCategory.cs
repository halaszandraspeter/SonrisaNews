namespace SonrisaNews.UnitTests;

/// <summary>
/// Test category constants used with <c>[Trait("Category", ...)]</c>. The
/// wave-2 verify command is <c>dotnet test --filter Category=Database</c>;
/// keeping the strings in one place means a typo fails at compile time,
/// not at runtime.
/// </summary>
internal static class DatabaseTestCategory
{
    public const string Database = "Database";
    public const string Cleanup = "Cleanup";
}
