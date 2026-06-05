namespace SonrisaNews.UnitTests;

/// <summary>
/// Test category constants for the worker-side test surface (pollers,
/// matchers, dispatcher — waves 6–8). The verify command for the
/// worker-integration slice is
/// <c>dotnet test --filter Category=WorkerIntegration</c>; sub-categories
/// pin the smaller scopes.
/// </summary>
internal static class WorkerIntegrationTestCategory
{
    public const string WorkerIntegration = "WorkerIntegration";
    public const string Matcher = "Matcher";
    public const string Sources = "Sources";
    public const string Poller = "Poller";
}
