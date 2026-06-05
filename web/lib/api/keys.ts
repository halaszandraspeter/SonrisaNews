/**
 * Shared React Query keys for the alert + channel queries.
 *
 * The query-key hierarchy is:
 *
 *   ["alerts"]                                — list (all)
 *   ["alerts", alertId]                       — single alert
 *   ["alerts", alertId, "channels"]           — channel-mode rows for an alert
 *   ["channels"]                              — caller's channel list
 *
 * Keys live in a single module so any feature can invalidate
 * the right slice without depending on a sibling feature's
 * internal hook file. Mutations invalidate the smallest set
 * needed (e.g. <c>setChannelMode</c> invalidates only
 * <c>["alerts", alertId, "channels"]</c>, not the whole
 * <c>["alerts"]</c> tree).
 */

export const alertsQueryKey = ["alerts"] as const;
export const alertQueryKey = (id: string) => ["alerts", id] as const;
export const alertChannelsQueryKey = (id: string) =>
  ["alerts", id, "channels"] as const;
export const channelsQueryKey = ["channels"] as const;
