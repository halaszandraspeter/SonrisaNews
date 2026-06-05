namespace SonrisaNews.Domain.Notifications;

/// <summary>
/// Outcome of a <see cref="INotificationChannel.SendAsync"/> call.
/// Always use the static factory methods (<see cref="Success"/>, <see cref="Failure"/>).
/// </summary>
public abstract record SendResult
{
    /// <summary>The send succeeded.</summary>
    public sealed record SuccessResult : SendResult;

    /// <summary>The send failed with a specific error.</summary>
    /// <param name="ErrorCode">A machine-readable error (e.g. "smtp_timeout", "webhook_404").</param>
    /// <param name="ErrorMessage">A human-readable error message (sanitized — no secrets).</param>
    public sealed record FailureResult(string ErrorCode, string ErrorMessage) : SendResult;

    /// <summary>The send succeeded.</summary>
    public static SendResult Success() => new SuccessResult();

    /// <summary>The send failed.</summary>
    public static SendResult Failure(string errorCode, string errorMessage) => new FailureResult(errorCode, errorMessage);
}
