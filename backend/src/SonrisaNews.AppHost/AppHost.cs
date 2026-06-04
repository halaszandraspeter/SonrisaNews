var builder = DistributedApplication.CreateBuilder(args);

// Backend services ----------------------------------------------------------------
// Aspire 13 picks up the API's own Kestrel endpoint from launchSettings.json,
// so we don't redeclare WithHttpEndpoint here — that would collide. The dashboard
// uses the project's default port (5080) and the Aspire service-discovery side
// uses the internal allocation.
//
// The yfinance sidecar is started by `scripts/dev.sh` / `dev.ps1`, not by the
// AppHost. We attach it in wave 7 once the C# MarketPoller exists and needs
// service discovery against it.

var api = builder.AddProject<Projects.SonrisaNews_Api>("api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var worker = builder.AddProject<Projects.SonrisaNews_Worker>("worker")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

builder.Build().Run();
