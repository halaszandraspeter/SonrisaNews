using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Infrastructure.Sources;
using SonrisaNews.Shared;

namespace SonrisaNews.Worker;

/// <summary>
/// Wave 6 — the news-poller background service. On each tick, the
/// poller builds a <see cref="NewsPollerRunner"/> from a fresh DI
/// scope and runs it. The runner iterates the registered
/// <see cref="IDataSource"/> instances (news-type), fetches a batch
/// from each, ingests the new events, and runs the news matcher for
/// each newly-inserted event.
/// </summary>
/// <remarks>
/// <para>
/// The polling cadence is fixed at 2 minutes (the wave-6 scope). A
/// future wave may switch to per-source
/// <see cref="IDataSource.PollInterval"/>; the seam is already in
/// place.
/// </para>
/// <para>
/// Reliability first: a throw inside the tick is caught and logged.
/// The hosted service loop survives transient errors so a single bad
/// source doesn't kill the worker.
/// </para>
/// </remarks>
public sealed class NewsPoller : BackgroundService
{
    /// <summary>Wave 6 polling cadence. Matches <see cref="IDataSource.PollInterval"/> for news sources.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NewsPoller> _logger;

    public NewsPoller(IServiceScopeFactory scopeFactory, ILogger<NewsPoller> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NewsPoller started; tick interval {Interval}", TickInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<NewsPollerRunner>();
                await runner.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failure in the per-tick plumbing (DB, DI) must NOT
                // kill the hosted service. The next tick will retry.
                _logger.LogError(ex, "NewsPoller tick failed; will retry in {Interval}", TickInterval);
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        _logger.LogInformation("NewsPoller stopping");
    }
}
