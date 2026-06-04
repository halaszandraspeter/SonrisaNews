using SonrisaNews.Shared;

namespace SonrisaNews.Worker;

/// <summary>Skeletal worker — placeholder for the pollers / matcher / dispatcher that land in waves 6–8.</summary>
public class SonrisaWorker(IClock clock, ILogger<SonrisaWorker> logger) : BackgroundService
{
    /// <summary>Wave-1 heartbeat interval. Replaced by per-source poll intervals in wave 6.</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SonrisaNews worker started at {Time}", clock.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            // LogTrace so the default Information-level output stays quiet, but the
            // Aspire dashboard / Serilog collector has something to show that the
            // loop is alive (not hung). Bump to LogDebug if you want it visible
            // in dev.
            logger.LogTrace("Heartbeat tick at {Time}", clock.UtcNow);

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown; let the while condition re-check.
                break;
            }
        }

        logger.LogInformation("SonrisaNews worker stopping at {Time}", clock.UtcNow);
    }
}