namespace SonrisaNews.Shared;

/// <summary>The UTC clock. Inject this in services instead of calling <see cref="DateTimeOffset.UtcNow"/> directly — production gets the real clock, tests get a fake.</summary>
public interface IClock
{
    /// <summary>Returns the current time in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>Default implementation that delegates to <see cref="DateTimeOffset.UtcNow"/>.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
