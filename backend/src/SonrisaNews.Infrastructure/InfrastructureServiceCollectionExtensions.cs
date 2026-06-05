using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Infrastructure.Channels;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Infrastructure.Sources;
using SonrisaNews.Shared;
using SonrisaNews.Shared.Hosting;

namespace SonrisaNews.Infrastructure;

/// <summary>Wires Infrastructure-layer services into DI. Call from both the API and Worker <c>Program.cs</c>.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSonrisaNewsInfrastructure(this IServiceCollection services)
    {
        services.AddSonrisaNewsClock();
        services.AddSonrisaNewsHttpClient();
        services.AddSonrisaNewsDbContext();
        services.AddSonrisaNewsChannels();
        services.AddSonrisaNewsSources();
        services.AddSonrisaNewsMatcher();
        return services;
    }

    /// <summary>
    /// Registers the typed <see cref="System.Net.Http.IHttpClientFactory"/> used by notification channels
    /// and data sources. The factory is a singleton; clients are short-lived and pooled internally.
    /// </summary>
    private static IServiceCollection AddSonrisaNewsHttpClient(this IServiceCollection services) =>
        services.AddHttpClient();

    /// <summary>Registers all notification channel implementations with keyed DI.</summary>
    private static IServiceCollection AddSonrisaNewsChannels(this IServiceCollection services)
    {
        services.AddKeyedScoped<INotificationChannel, EmailChannel>(ChannelTypeConstants.Email);
        services.AddKeyedScoped<INotificationChannel, SlackChannel>(ChannelTypeConstants.Slack);
        return services;
    }

    /// <summary>
    /// Registers the source-side services: the registry (DB-backed
    /// in prod), the per-source factories, and the dedupe ingest.
    /// Wave 6 ships <see cref="RssSource"/>; market and disaster
    /// sources land in waves 7 and 8.
    /// </summary>
    private static IServiceCollection AddSonrisaNewsSources(this IServiceCollection services)
    {
        services.AddScoped<EventIngestService>();
        services.AddScoped<IRssSourceFactory, RssSourceFactory>();
        services.AddScoped<ISourceRegistry, DbSourceRegistry>();
        return services;
    }

    /// <summary>
    /// Registers the news matcher. The matcher is a scoped service —
    /// it depends on the scoped <see cref="SonrisaNewsDbContext"/> and
    /// the <see cref="IClock"/>.
    /// </summary>
    private static IServiceCollection AddSonrisaNewsMatcher(this IServiceCollection services) =>
        services.AddScoped<NewsMatcher>()
                .AddScoped<INewsMatcher>(sp => sp.GetRequiredService<NewsMatcher>());
}