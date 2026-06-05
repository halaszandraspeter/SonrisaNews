using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SonrisaNews.Domain.Notifications;
using SonrisaNews.Infrastructure;

namespace SonrisaNews.UnitTests.Channels;

/// <summary>
/// DI resolution tests for the notification channel pipeline.
/// Pins the contract that <see cref="InfrastructureServiceCollectionExtensions.AddSonrisaNewsInfrastructure"/>
/// registers every implementation the dispatcher needs (IClock, keyed channels).
/// If a future change adds a constructor dependency to a channel without registering it
/// in DI, these tests fail at service-build time, not at request time.
/// </summary>
public class ChannelDiRegistrationTests
{
    [Fact]
    public void BuildServiceProvider_ResolvesEmailChannel_ByKey()
    {
        var services = new ServiceCollection();
        services.AddSonrisaNewsInfrastructure();

        using var sp = services.BuildServiceProvider();
        var channel = sp.GetKeyedService<INotificationChannel>(ChannelTypeConstants.Email);

        channel.Should().NotBeNull("the email channel must be registered as a keyed service in AddSonrisaNewsInfrastructure");
        channel!.Type.Should().Be(ChannelTypeConstants.Email);
    }

    [Fact]
    public void BuildServiceProvider_ResolvesSlackChannel_ByKey()
    {
        var services = new ServiceCollection();
        services.AddSonrisaNewsInfrastructure();

        using var sp = services.BuildServiceProvider();
        var channel = sp.GetKeyedService<INotificationChannel>(ChannelTypeConstants.Slack);

        channel.Should().NotBeNull("the slack channel must be registered as a keyed service in AddSonrisaNewsInfrastructure");
        channel!.Type.Should().Be(ChannelTypeConstants.Slack);
    }

    [Fact]
    public void BuildServiceProvider_ResolvesBothChannels_AsScopedInstances()
    {
        var services = new ServiceCollection();
        services.AddSonrisaNewsInfrastructure();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var email = scope.ServiceProvider.GetKeyedService<INotificationChannel>(ChannelTypeConstants.Email);
        var slack = scope.ServiceProvider.GetKeyedService<INotificationChannel>(ChannelTypeConstants.Slack);

        email.Should().NotBeNull();
        slack.Should().NotBeNull();
        email.Should().NotBeSameAs(slack, "each keyed service resolves to its own implementation");
    }
}
