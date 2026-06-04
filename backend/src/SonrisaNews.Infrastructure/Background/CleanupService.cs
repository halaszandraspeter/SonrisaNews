using Microsoft.Extensions.Logging;

namespace SonrisaNews.Infrastructure.Background;

/// <summary>
/// Stub of the wave-8 retention service. In wave 2 it just logs and does
/// nothing; in wave 8 it will execute the cleanup rules from
/// <c>1-features.md</c> §5: prune <c>Event &gt; 30d</c>,
/// <c>Notification &gt; 30d</c>, <c>AuditLog &gt; 90d</c>, and expired tokens.
/// </summary>
/// <remarks>
/// The wave-2 constructor takes only the logger. Wave 8 widens the
/// signature to take a <c>IDbContextFactory&lt;SonrisaNewsDbContext&gt;</c>
/// so each pass gets a fresh, short-lived context.
/// </remarks>
public class CleanupService
{
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(ILogger<CleanupService> logger)
    {
        _logger = logger;
    }

    /// <summary>Runs one cleanup pass. The wave-2 stub logs and returns; wave 8 adds the real retention rules.</summary>
    public Task RunOnceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Cleanup stub: no retention rules applied in wave 2. Wave 8 adds the 30/90-day cutoffs.");
        return Task.CompletedTask;
    }
}
