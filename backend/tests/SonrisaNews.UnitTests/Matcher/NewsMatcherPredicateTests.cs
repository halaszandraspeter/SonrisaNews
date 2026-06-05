using System;
using System.Text.Json;
using FluentAssertions;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;
using SonrisaNews.Domain.Entities;
using SonrisaNews.Infrastructure.Matcher;
using Xunit;

namespace SonrisaNews.UnitTests.Matcher;

/// <summary>
/// Direct unit tests for the pure <see cref="NewsMatcher.Matches"/>
/// predicate. The integration tests in <see cref="NewsMatcherTests"/>
/// exercise the same logic end-to-end through the DB; these tests
/// pin the predicate's behavior in isolation so the canonical
/// "what does the matcher do" reference is a 2-line grep away.
/// </summary>
/// <remarks>
/// The <see cref="NewsMatcher.Matches"/> method is <c>internal
/// static</c>; the test project reads it via the InternalsVisibleTo
/// pattern (the SonrisaNews.Infrastructure.csproj will need an
/// <c>InternalsVisibleTo("SonrisaNews.UnitTests")</c> entry — see
/// the wave-6 handoff §3.1 follow-up).
/// </remarks>
[Trait("Category", WorkerIntegrationTestCategory.Matcher)]
public class NewsMatcherPredicateTests
{
    // -- Keyword -----------------------------------------------------------

    [Fact]
    public void Matches_EmptyFilter_MatchesAnyEvent()
    {
        // The wave-5 handoff's "empty filter = off = match all"
        // convention, pinned here without DB round-trips.
        var filters = new NewsAlertFilters(null, null, KeywordMatchMode.All, null);
        var evt = NewEvent(title: "anything", summary: "anything", tags: Array.Empty<string>());

        NewsMatcher.Matches(filters, evt).Should().BeTrue();
    }

    [Fact]
    public void Matches_KeywordMatchesTitle_IsCaseInsensitive()
    {
        // The matcher's case-insensitive match is a documented
        // contract (see `Matches` impl). Assert it directly so a
        // future "switch to StringComparison.Ordinal" refactor
        // fails this test.
        var filters = new NewsAlertFilters(null, "BREXIT", KeywordMatchMode.All, null);
        var evt = NewEvent(title: "brexit news today", summary: "...", tags: Array.Empty<string>());

        NewsMatcher.Matches(filters, evt).Should().BeTrue();
    }

    [Fact]
    public void Matches_KeywordSearchesTitleAndSummary()
    {
        // The matcher checks `title`, `summary`, AND `description`
        // in the payload. A summary-only hit must still match.
        var filters = new NewsAlertFilters(null, "parliament", KeywordMatchMode.All, null);
        var inSummary = NewEvent(title: "Daily news", summary: "parliament in session", tags: Array.Empty<string>());

        NewsMatcher.Matches(filters, inSummary).Should().BeTrue(
            "the keyword is in the summary, not the title; both fields are searched");
    }

    [Fact]
    public void Matches_KeywordAbsent_DoesNotMatch()
    {
        var filters = new NewsAlertFilters(null, "brexit", KeywordMatchMode.All, null);
        var evt = NewEvent(title: "Markets fall", summary: "...", tags: Array.Empty<string>());

        NewsMatcher.Matches(filters, evt).Should().BeFalse();
    }

    [Fact]
    public void Matches_BlankKeyword_TreatedAsNoKeywordFilter()
    {
        // A blank keyword should behave like a null keyword: the
        // filter is "off", the event matches the other constraints
        // (or matches all, if those are also off).
        var filters = new NewsAlertFilters(null, "   ", KeywordMatchMode.All, null);
        var evt = NewEvent(title: "Anything", summary: "...", tags: Array.Empty<string>());

        NewsMatcher.Matches(filters, evt).Should().BeTrue();
    }

    // -- Tag list ---------------------------------------------------------

    [Fact]
    public void Matches_EmptyTagList_TreatedAsNoTagFilter()
    {
        var filters = new NewsAlertFilters(null, null, KeywordMatchMode.All, Array.Empty<string>());
        var evt = NewEvent(title: "Anything", summary: "...", tags: new[] { "ai" });

        NewsMatcher.Matches(filters, evt).Should().BeTrue();
    }

    [Fact]
    public void Matches_AllMode_RequiresEveryTag()
    {
        var filters = new NewsAlertFilters(null, null, KeywordMatchMode.All, new[] { "ai", "climate" });
        var both = NewEvent(title: "...", summary: "...", tags: new[] { "ai", "climate" });
        var one = NewEvent(title: "...", summary: "...", tags: new[] { "ai" });

        NewsMatcher.Matches(filters, both).Should().BeTrue();
        NewsMatcher.Matches(filters, one).Should().BeFalse();
    }

    [Fact]
    public void Matches_AnyMode_RequiresAtLeastOneTag()
    {
        var filters = new NewsAlertFilters(null, null, KeywordMatchMode.Any, new[] { "ai" });
        var matching = NewEvent(title: "...", summary: "...", tags: new[] { "ai" });
        var notMatching = NewEvent(title: "...", summary: "...", tags: new[] { "markets" });

        NewsMatcher.Matches(filters, matching).Should().BeTrue();
        NewsMatcher.Matches(filters, notMatching).Should().BeFalse();
    }

    [Fact]
    public void Matches_TagMatching_IsCaseInsensitive()
    {
        // Same case-insensitive contract as the keyword match.
        var filters = new NewsAlertFilters(null, null, KeywordMatchMode.All, new[] { "AI" });
        var evt = NewEvent(title: "...", summary: "...", tags: new[] { "ai" });

        NewsMatcher.Matches(filters, evt).Should().BeTrue();
    }

    [Fact]
    public void Matches_EventWithNoTags_FailsTagFilter()
    {
        // The "tag filter set but event has no tags" case must be
        // a non-match (the event has 0 tags; the filter requires ≥1).
        var filters = new NewsAlertFilters(null, null, KeywordMatchMode.Any, new[] { "ai" });
        var evt = NewEvent(title: "...", summary: "...", tags: Array.Empty<string>());

        NewsMatcher.Matches(filters, evt).Should().BeFalse();
    }

    // -- Source filter ----------------------------------------------------

    [Fact]
    public void Matches_SourceFilter_EventFromListedSource_Matches()
    {
        var sourceId = Guid.NewGuid();
        var filters = new NewsAlertFilters(new[] { sourceId }, null, KeywordMatchMode.All, null);
        var evt = NewEvent(title: "...", summary: "...", tags: Array.Empty<string>(), sourceId: sourceId);

        NewsMatcher.Matches(filters, evt).Should().BeTrue();
    }

    [Fact]
    public void Matches_SourceFilter_EventFromOtherSource_DoesNotMatch()
    {
        var listedSource = Guid.NewGuid();
        var otherSource = Guid.NewGuid();
        var filters = new NewsAlertFilters(new[] { listedSource }, null, KeywordMatchMode.All, null);
        var evt = NewEvent(title: "...", summary: "...", tags: Array.Empty<string>(), sourceId: otherSource);

        NewsMatcher.Matches(filters, evt).Should().BeFalse();
    }

    // -- Combined --------------------------------------------------------

    [Fact]
    public void Matches_KeywordAndTag_BothMustPass()
    {
        // Keyword matches but tags don't → no match.
        var filters = new NewsAlertFilters(null, "ai", KeywordMatchMode.All, new[] { "climate" });
        var wrongTags = NewEvent(title: "AI is exciting", summary: "...", tags: new[] { "markets" });
        NewsMatcher.Matches(filters, wrongTags).Should().BeFalse();

        // Both pass → match.
        var rightTags = NewEvent(title: "AI is exciting", summary: "...", tags: new[] { "climate" });
        NewsMatcher.Matches(filters, rightTags).Should().BeTrue();
    }

    // -- helpers ----------------------------------------------------------

    private static Event NewEvent(
        string title,
        string summary,
        string[] tags,
        Guid? sourceId = null)
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId ?? Guid.NewGuid(),
            ExternalId = Guid.NewGuid().ToString("N"),
            Type = AlertType.News,
            Payload = JsonSerializer.Serialize(new { title, summary, tags }),
            OccurredAt = DateTimeOffset.UtcNow,
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }
}
