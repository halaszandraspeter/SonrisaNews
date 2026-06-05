/**
 * Delivery-mode metadata. Mirrors the C# <c>DeliveryMode</c> enum
 * on the backend. Strings match the OpenAPI <c>paths</c> schema
 * (the backend's <c>System.Text.Json</c> serializes the enum
 * numeric value by default; the alert channel-mode endpoint
 * returns the string form because of <c>JsonStringEnumConverter</c>).
 *
 * Keep the order stable — the matrix renders in this order.
 */

export const DELIVERY_MODES = ["Realtime", "Digest15m", "DigestHourly", "DigestDaily"] as const;

export type DeliveryMode = (typeof DELIVERY_MODES)[number];

export const deliveryModeLabel: Record<DeliveryMode, string> = {
  Realtime: "Realtime",
  Digest15m: "Digest (15m)",
  DigestHourly: "Digest (hourly)",
  DigestDaily: "Digest (daily)",
};

/** Short hint shown under the dropdown. */
export const deliveryModeHint: Record<DeliveryMode, string> = {
  Realtime: "Send immediately when an event matches",
  Digest15m: "Batch matches, send every 15 minutes",
  DigestHourly: "Batch matches, send every hour",
  DigestDaily: "Batch matches, send once a day",
};
