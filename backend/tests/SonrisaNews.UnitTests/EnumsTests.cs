using FluentAssertions;
using SonrisaNews.Domain;
using Xunit;

namespace SonrisaNews.UnitTests;

public class EnumsTests
{
    [Fact]
    public void AlertType_HasThreeValues()
    {
        // The MVP supports exactly three alert types — News, Market, Disaster.
        // A new type is a deliberate schema change and must add a new enum value
        // + filter schema + matcher. This test guards against accidental drift.
        var values = Enum.GetValues<AlertType>();
        values.Should().HaveCount(3);
        values.Should().Contain(new[] { AlertType.News, AlertType.Market, AlertType.Disaster });
    }

    [Fact]
    public void DeliveryMode_HasFourModes()
    {
        // realtime + 3 digest cadences (15m / hourly / daily).
        Enum.GetValues<DeliveryMode>().Should().HaveCount(4);
    }

    [Fact]
    public void ChannelType_HasTwoImplementationsInMvp()
    {
        // Email + Slack are the only channels implemented in the MVP.
        // The interface is ready for more (SMS, push, webhook), but no others ship yet.
        Enum.GetValues<ChannelType>().Should().HaveCount(2);
    }
}
