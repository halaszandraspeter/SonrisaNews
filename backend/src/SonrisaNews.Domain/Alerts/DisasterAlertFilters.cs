namespace SonrisaNews.Domain.Alerts;

/// <summary>
/// Filter shape for a <see cref="Entities.Alert"/> of type <c>Disaster</c>.
/// A disaster event has a region (country or US-state code), an event type
/// (earthquake, hurricane, …), and a severity score. The matcher
/// (wave 8) drops events that fail any of these conditions.
/// </summary>
/// <param name="Regions">
/// Non-empty list of region codes — ISO 3166-1 alpha-2 for countries
/// (e.g. <c>"JP"</c>) or US state codes (e.g. <c>"US-CA"</c>). The
/// <c>"global"</c> sentinel means "any region". The list is OR-matched.
/// </param>
/// <param name="EventTypes">
/// Non-empty list of disaster event types (e.g. <c>"earthquake"</c>,
/// <c>"hurricane"</c>, <c>"flood"</c>). The list is OR-matched.
/// </param>
/// <param name="MinSeverity">
/// The minimum severity score to fire the alert. Scale is per event type
/// (Richter magnitude for earthquakes, Saffir-Simpson category for
/// hurricanes, etc.). Must be a non-negative finite value.
/// </param>
public sealed record DisasterAlertFilters(
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> EventTypes,
    double MinSeverity);
