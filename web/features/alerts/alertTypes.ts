/**
 * Alert-type metadata. The single source of truth for the alert
 * type labels, icons, and filter-schema references used by the
 * editor UI. Keeps the editor's "what alert is this?" branching
 * data-driven.
 */

export const ALERT_TYPES = ["News", "Market", "Disaster"] as const;

export type AlertType = (typeof ALERT_TYPES)[number];

export const alertTypeLabel: Record<AlertType, string> = {
  News: "News",
  Market: "Market",
  Disaster: "Disaster",
};

export const alertTypeOrder: ReadonlyArray<AlertType> = ["News", "Market", "Disaster"];

/** Empty filter document per type. Editor starts from this. */
export const emptyFiltersFor = (type: AlertType): string => {
  switch (type) {
    case "News":
      return JSON.stringify({ sourceIds: [], keyword: null, matchMode: "All", tags: [] });
    case "Market":
      return JSON.stringify({ symbols: [], percentThreshold: 5, windowMinutes: 60 });
    case "Disaster":
      return JSON.stringify({ regions: [], eventTypes: [], minSeverity: 5 });
  }
};
