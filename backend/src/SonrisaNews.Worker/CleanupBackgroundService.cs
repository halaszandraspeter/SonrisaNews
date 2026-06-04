using Microsoft.Extensions.Logging;
using SonrisaNews.Infrastructure.Background;

namespace SonrisaNews.Worker;

/// <summary>
/// Wave 2 — wraps the (stub) <see cref="CleanupService"/> in the worker's
/// host lifecycle so the service runs on a timer. The retention rules land
/// in wave 8 per <c>docs/implementation/mvp-checklist.md</c> §2; for now the
/// body is a no-op and the loop is just a heartbeat.
/// </summary>
public class CleanupBackgroundService(ILogger<CleanupBackgroundService> logger) : BackgroundService
{
    /// <summary>Wave-2 stub interval. Wave 8 changes this to 1 hour per <c>1-features.md</c> §5.</summary>
    private static readonly TimeSpan StubInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Inline a typed ILogger<CleanupService> for the service. The service
        // does its own structured logging and the category name surfaces in
        // Serilog output, so the production log shape is consistent.
        var serviceLogger = new TypedForwarder(logger);
        var cleanup = new CleanupService(serviceLogger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await cleanup.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Reliability first: a single failed pass must not kill the
                // background loop. Wave 8 will route this through the admin
                // health page; for now, just log.
                logger.LogError(ex, "Cleanup pass failed");
            }

            try
            {
                await Task.Delay(StubInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Re-categorises an <see cref="ILogger{T}"/> so the inner service sees
    /// the right generic type. One allocation per host start.
    /// </summary>
    private sealed class TypedForwarder(ILogger<CleanupBackgroundService> inner) : ILogger<CleanupService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
