namespace SonrisaNews.Domain;

/// <summary>The category of an alert. Determines which <see cref="Filters"/> schema applies.</summary>
public enum AlertType
{
    News = 1,
    Market = 2,
    Disaster = 3,
}

/// <summary>How a match is delivered to a channel.</summary>
public enum DeliveryMode
{
    Realtime = 1,
    Digest15m = 2,
    DigestHourly = 3,
    DigestDaily = 4,
}

/// <summary>The type of a notification channel.</summary>
public enum ChannelType
{
    Email = 1,
    Slack = 2,
}

/// <summary>The category of an upstream data source.</summary>
public enum SourceType
{
    News = 1,
    MarketSymbol = 2,
    Disaster = 3,
}

/// <summary>Status of a user account.</summary>
public enum UserStatus
{
    Active = 1,
    PendingEmailVerification = 2,
    Suspended = 3,
    SoftDeleted = 4,
}

/// <summary>Role assigned to a user. Drives RBAC checks.</summary>
public enum UserRole
{
    User = 1,
    Admin = 2,
    System = 3,
}
