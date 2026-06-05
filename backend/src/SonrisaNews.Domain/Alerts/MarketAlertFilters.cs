namespace SonrisaNews.Domain.Alerts;

/// <summary>
/// Filter shape for a <see cref="Entities.Alert"/> of type <c>Market</c>.
/// Defines a list of ticker symbols and a percent-change threshold over a
/// rolling window. The matcher (wave 7) evaluates each new quote against
/// this shape.
/// </summary>
/// <param name="Symbols">
/// Non-empty list of ticker symbols (e.g. <c>AAPL</c>, <c>MSFT</c>). At
/// least one symbol is required.
/// </param>
/// <param name="PercentThreshold">
/// The minimum absolute percent change in <paramref name="WindowMinutes"/>
/// to fire the alert. Must be a positive finite value.
/// </param>
/// <param name="WindowMinutes">
/// The rolling window in minutes over which the percent change is
/// measured. Must be a positive integer.
/// </param>
public sealed record MarketAlertFilters(
    IReadOnlyList<string> Symbols,
    double PercentThreshold,
    int WindowMinutes);
