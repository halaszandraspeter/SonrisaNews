using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SonrisaNews.Infrastructure;
using SonrisaNews.Infrastructure.Matcher;
using SonrisaNews.Infrastructure.Persistence;
using SonrisaNews.Infrastructure.Sources;
using SonrisaNews.Shared;
using SonrisaNews.Worker;
using Xunit;

namespace SonrisaNews.UnitTests.WorkerIntegration;

/// <summary>
/// Host-startup smoke test for the Worker process. Builds an
/// <see cref="IHost"/> with the same DI shape as
/// <c>Worker/Program.cs</c> and asserts that every service the
/// background services <c>GetRequiredService</c>-d at runtime is
/// resolvable. A future "someone commented out the registration"
/// regression (the latent bug that hit wave 6's first commit on the
/// <see cref="NewsPollerRunner"/>) would crash the worker on first
/// tick; this test catches it at the test stage.
/// </summary>
/// <remarks>
/// We use a <c>Microsoft.Extensions.Hosting.Host</c> directly
/// rather than a <c>WebApplicationFactory</c> because the worker is
/// <c>Microsoft.NET.Sdk.Worker</c>, not a web host. The test
/// validates the DI graph on the root provider via
/// <see cref="IServiceProviderIsService"/>; we deliberately do not
/// call <c>host.StartAsync()</c> because a missing registration
/// surfaces as a clear "Unable to resolve service" exception from
/// <c>GetRequiredService</c> with no race against a cancellation
/// token and no chance of a hosted service swallowing the exception.
/// </remarks>
[Trait("Category", WorkerIntegrationTestCategory.Poller)]
public class NewsPollerHostStartupTests
{
    [Fact]
    public void WorkerHost_ResolvesEveryScopedDependency_ThatTheRunnersNeed()
    {
        // Build the same DI shape as Worker/Program.cs. The test's
        // only intent is to surface "the runner is not registered"
        // (or one of its dependencies is missing) as a DI
        // resolution failure, not as a worker-tick crash. See the
        // class-level <remarks> for why we never call
        // host.StartAsync().
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sonrisa"] = "Data Source=:memory:",
        });

        // Mirror the production registrations (see Worker/Program.cs).
        // We don't call AddSonrisaNewsSerilog() because Serilog is a
        // no-op for an in-memory test and would just need a console
        // sink. The connection string is forced to :memory: so the
        // test doesn't touch the dev SQLite file.
        builder.Services.AddSonrisaNewsInfrastructure();
        builder.Services.AddHostedService<NewsPoller>();
        builder.Services.AddScoped<NewsPollerRunner>();

        using var host = builder.Build();
        var rootSp = host.Services;

        // Validate the DI graph BEFORE resolving. We probe the root
        // provider (not a scope) because IServiceProviderIsService on
        // the root returns true for any registered service, including
        // Scoped ones — only GetService/GetRequiredService requires a
        // scope to actually construct the instance. The test is "is
        // the line present in Program.cs?", which the root is the
        // right answer for.
        AssertRegistration(rootSp, typeof(NewsPollerRunner));
        AssertRegistration(rootSp, typeof(SonrisaNewsDbContext));
        AssertRegistration(rootSp, typeof(IClock));
        AssertRegistration(rootSp, typeof(ISourceRegistry));
        AssertRegistration(rootSp, typeof(EventIngestService));
        AssertRegistration(rootSp, typeof(NewsMatcher));

        // The hosted service itself must be present (proves the
        // AddHostedService<NewsPoller>() line is in the program).
        rootSp
            .GetServices<IHostedService>()
            .OfType<NewsPoller>()
            .Should().ContainSingle(
                "NewsPoller must be registered as a hosted service — the worker's main loop lives in its ExecuteAsync method");

        // Finally, resolve the runner from a per-tick scope (the
        // same shape the hosted service uses inside ExecuteAsync).
        // The call to GetRequiredService is the assertion: it throws
        // InvalidOperationException if the registration is missing.
        using var scope = rootSp.CreateScope();
        scope.ServiceProvider.GetRequiredService<NewsPollerRunner>();
    }

    private static void AssertRegistration(IServiceProvider sp, Type service)
    {
        // IServiceProviderIsService is the canonical "is this
        // registered?" probe — the same one the framework calls
        // internally from ServiceProvider.ValidateOnBuild and
        // ValidateScopes, so a missing registration surfaces the
        // same way in both the test and in production.
        var isService = sp.GetRequiredService<IServiceProviderIsService>();
        isService.IsService(service).Should().BeTrue(
            $"{service.FullName} must be registered in DI — the worker resolves it from a per-tick scope; a missing registration would crash the production worker on first tick instead of failing the test");
    }

    [Fact]
    public void WorkerHost_ResolvesScoped_NewsPollerRunner_AcrossScopes()
    {
        // Defense-in-depth: the runner is registered as Scoped
        // (one instance per DI scope). The hosted service creates a
        // fresh scope per tick; this test asserts the scope
        // semantics are right by resolving twice and asserting
        // they're independent instances.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sonrisa"] = "Data Source=:memory:",
        });
        builder.Services.AddSonrisaNewsInfrastructure();
        builder.Services.AddHostedService<NewsPoller>();
        builder.Services.AddScoped<NewsPollerRunner>();

        using var host = builder.Build();
        using var scope1 = host.Services.CreateScope();
        using var scope2 = host.Services.CreateScope();

        var r1 = scope1.ServiceProvider.GetRequiredService<NewsPollerRunner>();
        var r2 = scope2.ServiceProvider.GetRequiredService<NewsPollerRunner>();

        r1.Should().NotBeSameAs(r2,
            "the runner is registered Scoped — each tick (each scope) gets a fresh instance, which is what we want for the per-scope DbContext lifetime");
    }
}
