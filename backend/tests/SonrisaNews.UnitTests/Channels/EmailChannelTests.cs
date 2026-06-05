using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Infrastructure.Channels;
using SonrisaNews.Shared;

namespace SonrisaNews.UnitTests.Channels;

public class EmailChannelTests
{
    private readonly ILogger<EmailChannel> _logger;
    private readonly Mock<IHttpClientFactory> _httpClientFactory;
    private readonly FakeClock _clock;

    public EmailChannelTests()
    {
        _logger = new Mock<ILogger<EmailChannel>>().Object;
        _httpClientFactory = new Mock<IHttpClientFactory>();
        _clock = new FakeClock();
    }

    [Fact]
    public void Type_ReturnsEmailConstant()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        channel.Type.Should().Be(ChannelTypeConstants.Email);
    }

    [Fact]
    public async Task StartVerificationAsync_GeneratesSixDigitCode()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);

        var challenge = await channel.StartVerificationAsync("test@example.com", CancellationToken.None);

        challenge.Code.Should().NotBeNullOrEmpty();
        challenge.Code.Should().HaveLength(6);
        challenge.Code.Should().MatchRegex(@"^\d{6}$");
    }

    [Fact]
    public async Task StartVerificationAsync_StampsExpiry_24HoursFromIssue()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);

        var challenge = await channel.StartVerificationAsync("test@example.com", CancellationToken.None);

        challenge.ExpiresAt.Should().Be(_clock.UtcNow.AddHours(24));
    }

    [Fact]
    public async Task VerifyAsync_CorrectCode_ReturnsTrue()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "test@example.com";

        var challenge = await channel.StartVerificationAsync(destination, CancellationToken.None);
        var result = await channel.VerifyAsync(destination, challenge.Code, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WrongCode_ReturnsFalse()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "test@example.com";

        await channel.StartVerificationAsync(destination, CancellationToken.None);
        var result = await channel.VerifyAsync(destination, "000000", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_After24Hours_ReturnsFalse()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "test@example.com";

        var challenge = await channel.StartVerificationAsync(destination, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));
        var result = await channel.VerifyAsync(destination, challenge.Code, CancellationToken.None);

        result.Should().BeFalse("a verification code should expire after 24h per the MVP checklist Q3 default");
    }

    [Fact]
    public async Task VerifyAsync_JustBefore24Hours_ReturnsTrue()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "test@example.com";

        var challenge = await channel.StartVerificationAsync(destination, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)));
        var result = await channel.VerifyAsync(destination, challenge.Code, CancellationToken.None);

        result.Should().BeTrue("a verification code is still valid at 23h59m");
    }

    [Fact]
    public async Task StartVerificationAsync_NewCode_ReplacesExpiredOne()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var destination = "test@example.com";

        var first = await channel.StartVerificationAsync(destination, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));
        var second = await channel.StartVerificationAsync(destination, CancellationToken.None);
        var result = await channel.VerifyAsync(destination, second.Code, CancellationToken.None);

        result.Should().BeTrue("re-issuing a code after expiry gives a fresh, valid code");
        first.Code.Should().NotBe(second.Code);
    }

    [Fact]
    public async Task SendAsync_WithValidPayload_ReturnsSuccess()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var payload = new NotificationPayload(
            NotificationId: Guid.NewGuid(),
            AlertId: Guid.NewGuid(),
            AlertName: "Test Alert",
            ChannelId: Guid.NewGuid(),
            Destination: "user@example.com",
            MatchSummary: "Test match occurred",
            OccurredAt: DateTimeOffset.UtcNow
        );

        var result = await channel.SendAsync(payload, CancellationToken.None);

        result.Should().BeOfType<SendResult.SuccessResult>();
    }

    [Fact]
    public async Task SendAsync_InvalidEmail_ReturnsFailure()
    {
        var channel = new EmailChannel(_httpClientFactory.Object, _logger, _clock);
        var payload = new NotificationPayload(
            NotificationId: Guid.NewGuid(),
            AlertId: Guid.NewGuid(),
            AlertName: "Test Alert",
            ChannelId: Guid.NewGuid(),
            Destination: "not-an-email",
            MatchSummary: "Test match occurred",
            OccurredAt: DateTimeOffset.UtcNow
        );

        var result = await channel.SendAsync(payload, CancellationToken.None);

        result.Should().BeOfType<SendResult.FailureResult>();
        var failure = (SendResult.FailureResult)result;
        failure.ErrorCode.Should().NotBeNullOrEmpty();
        failure.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

/// <summary>
/// In-memory <see cref="IClock"/> for tests. Tests advance time with
/// <see cref="Advance"/>, so verification expiry can be exercised deterministically.
/// </summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
