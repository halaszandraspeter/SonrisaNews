using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Infrastructure.Channels;
using SonrisaNews.Infrastructure.Persistence;
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
        return services;
    }

    /// <summary>
    /// Registers the typed <see cref="System.Net.Http.IHttpClientFactory"/> used by notification channels
    /// (Slack webhook posts, future HTTP-based channels). The factory is a singleton; clients are
    /// short-lived and pooled internally. Channels call
    /// <c>_httpClientFactory.CreateClient(name)</c> — Polly v8 resilience can be layered per-channel
    /// in a follow-up via <c>AddHttpClient&lt;TClient&gt;(...)</c> named-client registrations.
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
}