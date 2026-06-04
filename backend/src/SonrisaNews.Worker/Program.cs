using SonrisaNews.Infrastructure;
using SonrisaNews.Shared.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Services.AddSonrisaNewsSerilog();

builder.Services.AddHostedService<SonrisaNews.Worker.SonrisaWorker>();
builder.Services.AddSonrisaNewsInfrastructure();

var host = builder.Build();
host.Run();