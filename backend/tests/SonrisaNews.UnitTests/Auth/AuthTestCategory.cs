namespace SonrisaNews.UnitTests.Auth;

/// <summary>
/// Test category constants for the Auth test surface. The wave 3 verify
/// command is <c>dotnet test --filter Category=Auth</c>; the
/// <see cref="Seeder"/> sub-category exists so a future implementer can
/// run only seeder tests when the seeder changes.
/// </summary>
internal static class AuthTestCategory
{
    public const string Auth = "Auth";
    public const string Seeder = "Seeder";
    public const string Service = "Service";
    public const string Handler = "Handler";
    public const string Rbac = "Rbac";
}
