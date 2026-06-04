using Microsoft.Extensions.DependencyInjection;
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
        services.AddSonrisaNewsDbContext();
        return services;
    }
}