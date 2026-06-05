using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Infrastructure.Persistence;

namespace SonrisaNews.Infrastructure.Alerts;

/// <summary>DI registration for the alert service. The API host calls this once in <c>Program.cs</c>.</summary>
public static class AlertServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IAlertService"/> to DI. The implementation uses
    /// a scoped <see cref="SonrisaNewsDbContext"/> (same as
    /// <c>ChannelsController</c>) and an injected <c>IClock</c>; both
    /// are already registered by <c>AddSonrisaNewsInfrastructure()</c>.
    /// </summary>
    public static IServiceCollection AddSonrisaNewsAlerts(this IServiceCollection services) =>
        services.AddScoped<IAlertService, AlertService>();
}
