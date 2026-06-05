namespace SonrisaNews.Domain.Notifications;

/// <summary>
/// String constants for channel types. These match the <see cref="ChannelType"/> enum values
/// and are used as the DI keyed registration names.
/// </summary>
public static class ChannelTypeConstants
{
    public const string Email = "email";
    public const string Slack = "slack";
}
