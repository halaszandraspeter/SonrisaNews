using Microsoft.Extensions.DependencyInjection;

namespace SonrisaNews.Shared.Hosting;

/// <summary>DI registration for <see cref="IClock"/>. Shared by the API and Worker.</summary>
public static class ApplicationClockServiceCollectionExtensions
{
    /// <summary>
    /// Registers the UTC clock as a singleton. <see cref="IClock"/> is stateless and
    /// thread-safe; a single instance is shared by all consumers.
    /// </summary>
    public static IServiceCollection AddSonrisaNewsClock(this IServiceCollection services) =>
        services.AddSingleton<IClock, SystemClock>();
}