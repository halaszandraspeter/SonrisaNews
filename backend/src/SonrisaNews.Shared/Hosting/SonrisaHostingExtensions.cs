using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SonrisaNews.Shared.Hosting;

/// <summary>
/// Cross-process host defaults. Called from both the API and Worker <c>Program.cs</c>
/// so they emit identical log shapes. Future host-level cross-cutting concerns
/// (ProblemDetails, health checks, CORS) land here too.
/// </summary>
public static class SonrisaHostingExtensions
{
    /// <summary>
    /// Wires Serilog with the same configuration shape for the API and the Worker.
    /// Uses <c>IServiceCollection.AddSerilog</c> (from <c>Serilog.Extensions.Hosting</c>)
    /// rather than <c>IHostBuilder.UseSerilog</c> so the same code path works for
    /// both the Web host (<c>WebApplicationBuilder</c>) and the generic host
    /// (<c>HostApplicationBuilder</c>). Reads from <c>Serilog</c> in
    /// <c>appsettings.json</c>, enriches with <c>FromLogContext</c>, and writes
    /// structured JSON to the console (one event per line, invariant culture).
    /// </summary>
    public static IServiceCollection AddSonrisaNewsSerilog(this IServiceCollection services) =>
        services.AddSerilog((sp, configuration) => configuration
            .ReadFrom.Configuration(sp.GetRequiredService<IConfiguration>())
            .ReadFrom.Services(sp)
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));
}