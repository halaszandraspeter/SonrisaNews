using SonrisaNews.Infrastructure;
using SonrisaNews.Shared.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Services.AddSonrisaNewsSerilog();

builder.Services.AddHostedService<SonrisaNews.Worker.SonrisaWorker>();
builder.Services.AddHostedService<SonrisaNews.Worker.CleanupBackgroundService>();
// Wave 6: the news poller is the first non-stub hosted service in
// the worker. It iterates the registered IDataSource instances, fetches
// each on a 2-minute cadence, persists dedup'd events, and runs the
// news matcher. Market and disaster pollers land in waves 7 and 8.
//
// The hosted service creates a fresh DI scope per tick and resolves
// NewsPollerRunner from it. Two registrations are required:
//   1. AddHostedService<NewsPoller>() — the long-running loop.
//   2. AddScoped<NewsPollerRunner>()  — the per-tick work the loop
//      resolves. SKIP THIS and the worker throws on first tick
//      (the GetRequiredService inside ExecuteAsync fails). The
//      runner's own dependencies (matcher + ingest + registry) are
//      all added by AddSonrisaNewsInfrastructure() below; this line
//      only registers the runner itself.
// The runner is scoped (not singleton) because its DbContext
// dependency is scoped per DI scope.
builder.Services.AddHostedService<SonrisaNews.Worker.NewsPoller>();
builder.Services.AddScoped<SonrisaNews.Worker.NewsPollerRunner>();
builder.Services.AddSonrisaNewsInfrastructure();

var host = builder.Build();
host.Run();