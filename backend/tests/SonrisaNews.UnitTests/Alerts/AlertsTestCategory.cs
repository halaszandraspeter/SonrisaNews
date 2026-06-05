namespace SonrisaNews.UnitTests.Alerts;

/// <summary>
/// Test category constants for the alerts test surface. The wave 5 verify
/// command is <c>dotnet test --filter Category=Alerts</c>; sub-categories
/// pin the smaller scopes.
/// </summary>
internal static class AlertsTestCategory
{
    public const string Alerts = "Alerts";
    public const string Serializer = "Serializer";
    public const string Service = "Service";
    public const string Controller = "Controller";
}
