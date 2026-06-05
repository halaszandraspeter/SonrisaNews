using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Infrastructure.Channels;
using SonrisaNews.Shared;

namespace SonrisaNews.UnitTests.Channels;

public class SlackChannelTests
{
    private readonly ILogger<SlackChannel> _logger;
    private readonly Mock<IHttpClientFactory> _httpClientFactory;
    private readonly FakeClock _clock;

    public SlackChannelTests()
    {
        _logger = new Mock<ILogger<SlackChannel>>().Object;
        _httpClientFactory = new Mock<IHttpClientFactory>();
        _clock = new FakeClock();
    }

    [Fact]
    public void Type_ReturnsSlackConstant()
    {
        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);
        channel.Type.Should().Be(ChannelTypeConstants.Slack);
    }

    [Fact]
    public async Task StartVerificationAsync_GeneratesChallengeCode()
    {
        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);

        var challenge = await channel.StartVerificationAsync("https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX", CancellationToken.None);

        challenge.Code.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartVerificationAsync_StampsExpiry_24HoursFromIssue()
    {
        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);

        var challenge = await channel.StartVerificationAsync("https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX", CancellationToken.None);

        challenge.ExpiresAt.Should().Be(_clock.UtcNow.AddHours(24));
    }

    [Fact]
    public async Task VerifyAsync_WithStoredCode_ReturnsTrue()
    {
        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX";

        var challenge = await channel.StartVerificationAsync(destination, CancellationToken.None);
        var result = await channel.VerifyAsync(destination, challenge.Code, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WithWrongCode_ReturnsFalse()
    {
        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX";

        await channel.StartVerificationAsync(destination, CancellationToken.None);
        var result = await channel.VerifyAsync(destination, "wrong-code", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_After24Hours_ReturnsFalse()
    {
        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX";

        var challenge = await channel.StartVerificationAsync(destination, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));
        var result = await channel.VerifyAsync(destination, challenge.Code, CancellationToken.None);

        result.Should().BeFalse("a verification code should expire after 24h per the MVP checklist Q3 default");
    }

    [Fact]
    public async Task SendAsync_WithValidPayload_PostsToWebhook()
    {
        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("ok") })
            .Verifiable();

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);
        var payload = new NotificationPayload(
            NotificationId: Guid.NewGuid(),
            AlertId: Guid.NewGuid(),
            AlertName: "Test Alert",
            ChannelId: Guid.NewGuid(),
            Destination: "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX",
            MatchSummary: "Test match occurred",
            OccurredAt: DateTimeOffset.UtcNow
        );

        var result = await channel.SendAsync(payload, CancellationToken.None);

        result.Should().BeOfType<SendResult.SuccessResult>();
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task SendAsync_WebhookReturnsError_ReturnsFailure()
    {
        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound))
            .Verifiable();

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var channel = new SlackChannel(_httpClientFactory.Object, _logger, _clock);
        var payload = new NotificationPayload(
            NotificationId: Guid.NewGuid(),
            AlertId: Guid.NewGuid(),
            AlertName: "Test Alert",
            ChannelId: Guid.NewGuid(),
            Destination: "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXX",
            MatchSummary: "Test match occurred",
            OccurredAt: DateTimeOffset.UtcNow
        );

        var result = await channel.SendAsync(payload, CancellationToken.None);

        result.Should().BeOfType<SendResult.FailureResult>();
    }
}
