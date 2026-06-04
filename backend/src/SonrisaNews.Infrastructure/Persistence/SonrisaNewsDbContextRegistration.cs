using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Shared;

namespace SonrisaNews.Infrastructure.Persistence;

/// <summary>DI helpers for the EF Core context. Call from the API and Worker <c>Program.cs</c>.</summary>
public static class SonrisaNewsDbContextRegistration
{
    /// <summary>
    /// Registers the DbContext against SQLite. The connection string is read from
    /// <see cref="ConfigurationKeys.SonrisaDatabase"/> under <see cref="ConfigurationKeys.ConnectionStrings"/>.
    /// </summary>
    /// <remarks>
    /// Resolution order (highest priority first): environment variable
    /// (<c>ConnectionStrings__Sonrisa</c>), <c>dotnet user-secrets</c>, then
    /// <c>appsettings.{Environment}.json</c>. In dev, the appsettings default
    /// points at <c>../../data/sonrisa.db</c> relative to the project hosting the
    /// config (i.e. <c>data/sonrisa.db</c> at the repo root).
    /// </remarks>
    public static IServiceCollection AddSonrisaNewsDbContext(this IServiceCollection services)
    {
        services.AddDbContext<SonrisaNewsDbContext>((sp, options) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(ConfigurationKeys.SonrisaDatabase);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Missing connection string '{ConfigurationKeys.SonrisaDatabase}' under " +
                    $"'{ConfigurationKeys.ConnectionStrings}'. " +
                    $"Provide it via the env var 'ConnectionStrings__{ConfigurationKeys.SonrisaDatabase}', " +
                    $"`dotnet user-secrets` (project: SonrisaNews.Infrastructure), " +
                    $"or `appsettings.{ConfigurationKeys.SonrisaDatabase}.json`.");
            }

            options.UseSqlite(connectionString);
        });

        return services;
    }
}