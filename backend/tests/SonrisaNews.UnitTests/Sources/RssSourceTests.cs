using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Sources;
using SonrisaNews.Infrastructure.Sources;
using Xunit;

namespace SonrisaNews.UnitTests.Sources;

/// <summary>
/// Unit tests for <see cref="RssSource"/>. Per the <c>add-a-data-source</c>
/// skill, every new <see cref="IDataSource"/> implementation must ship
/// with at least two tests:
/// <list type="number">
///   <item>Parses a canned upstream response into the expected
///         <see cref="RawEvent"/> shape.</item>
///   <item>Returns an empty list (does not throw) on a 5xx.</item>
/// </list>
/// These tests are the wave-6 instantiation of that contract.
/// </summary>
[Trait("Category", WorkerIntegrationTestCategory.Sources)]
public class RssSourceTests
{
    [Fact]
    public void Type_IsNews()
    {
        var source = NewSource(NewHandler("""<rss version="2.0"><channel><title>Test</title></channel></rss>"""));
        source.Type.Should().Be(SourceType.News);
    }

    [Fact]
    public void PollInterval_IsTwoMinutes()
    {
        var source = NewSource(NewHandler("""<rss><channel><title>x</title></channel></rss>"""));
        source.PollInterval.Should().Be(TimeSpan.FromMinutes(2),
            "the mvp-checklist wave 6 scope pins the news poll interval at 2 minutes");
    }

    [Fact]
    public async Task FetchAsync_ParsesItems_IntoRawEvents()
    {
        // Canned RSS 2.0 response with two <item> entries. The
        // <guid> becomes ExternalId; <title> + <description> go into
        // the payload JSON; <pubDate> becomes OccurredAt.
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>Test Feed</title>
                <item>
                  <title>First headline</title>
                  <description>First body.</description>
                  <link>https://example.com/1</link>
                  <guid>https://example.com/1</guid>
                  <pubDate>Fri, 05 Jun 2026 12:00:00 GMT</pubDate>
                </item>
                <item>
                  <title>Second headline</title>
                  <description>Second body.</description>
                  <link>https://example.com/2</link>
                  <guid>https://example.com/2</guid>
                  <pubDate>Fri, 05 Jun 2026 12:30:00 GMT</pubDate>
                </item>
              </channel>
            </rss>
            """;
        var source = NewSource(NewHandler(xml));

        var events = await source.FetchAsync(CancellationToken.None);

        events.Should().HaveCount(2);
        events[0].ExternalId.Should().Be("https://example.com/1");
        events[0].Type.Should().Be(AlertType.News);
        events[0].Payload.Should().Contain("First headline");
        events[0].Payload.Should().Contain("First body.");
        events[1].ExternalId.Should().Be("https://example.com/2");
    }

    [Fact]
    public async Task FetchAsync_EmptyChannel_ReturnsEmptyList()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>Test Feed</title>
              </channel>
            </rss>
            """;
        var source = NewSource(NewHandler(xml));

        var events = await source.FetchAsync(CancellationToken.None);

        events.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_Http5xx_ReturnsEmptyList_DoesNotThrow()
    {
        // The "does not throw on transient failure" contract from
        // IDataSource.XML doc. The poller's per-source loop depends on
        // this — a throw kills the worker.
        var source = NewSource(NewStatusHandler(HttpStatusCode.InternalServerError));
        var act = () => source.FetchAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        var events = await act();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_MalformedXml_ReturnsEmptyList_DoesNotThrow()
    {
        // The poller is the same shape for an HTTP error or a parse
        // error: empty list, no throw. The matcher simply gets no new
        // events this round and the next poll will retry.
        var source = NewSource(NewHandler("not-xml-at-all"));
        var events = await source.FetchAsync(CancellationToken.None);

        events.Should().BeEmpty();
    }

    // -- helpers ------------------------------------------------------------

    private static RssSource NewSource(HttpMessageHandler handler) =>
        new(new StaticHandlerFactory(handler), "https://example.com/feed.xml", "test-feed");

    private sealed class StaticHandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpMessageHandler NewHandler(string body) => new StringHandler(body, HttpStatusCode.OK);
    private static HttpMessageHandler NewStatusHandler(HttpStatusCode status) => new StringHandler(string.Empty, status);

    private sealed class StringHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/rss+xml"),
            });
    }
}
