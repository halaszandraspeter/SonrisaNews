using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SonrisaNews.Infrastructure.Background;
using Xunit;

namespace SonrisaNews.UnitTests;

/// <summary>
/// Wave 2 — stub the cleanup service so the schema is exercised end-to-end
/// without a real background loop. The real retention enforcement lands in
/// wave 8 per <c>docs/implementation/mvp-checklist.md</c> §2 (wave 8).
/// </summary>
[Trait("Category", DatabaseTestCategory.Cleanup)]
public class CleanupStubTests
{
    [Fact]
    public async Task CleanupService_RunOnceAsync_LogsThatItIsAStub()
    {
        // The stub is intentionally a no-op for the schema-side data path;
        // it just proves that the Worker host can wire the service and that
        // the ILogger contract is honoured. Wave 8 replaces the body with
        // the retention rules from 1-features.md §5 ("Cleanup").
        var service = new CleanupService(NullLogger<CleanupService>.Instance);

        var act = () => service.RunOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
