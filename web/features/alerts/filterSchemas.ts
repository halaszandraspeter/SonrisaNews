/**
 * Zod schemas for the <c>Alert.Filters</c> JSON column. Mirrors the
 * backend's typed DTOs in
 * <c>backend/src/SonrisaNews.Domain/Alerts/NewsAlertFilters.cs</c>
 * (and the Market/Disaster siblings). The backend's
 * <c>AlertFiltersSerializer</c> is the schema-validity tripwire: it
 * rejects unknown properties and per-type structural errors. The
 * Zod schemas here replicate the same shape so the editor surfaces
 * the same errors client-side before the user clicks "Save".
 *
 * The editor always round-trips through a JSON string (the
 * backend stores filters as <c>TEXT</c> on SQLite / <c>jsonb</c> on
 * Postgres). The schema parses the parsed value, then the form
 * re-serializes to canonical JSON via <c>canonicalize</c>.
 */

import { z } from "zod";

import { type AlertType } from "./alertTypes";

const guidSchema = z.string().uuid();

/** News filter: optional source ids, optional keyword, optional tag list. */
export const newsFiltersSchema = z
  .object({
    sourceIds: z.array(guidSchema).default([]),
    keyword: z.string().nullable().default(null),
    matchMode: z.enum(["All", "Any"]).default("All"),
    tags: z.array(z.string().min(1).max(50)).default([]),
  })
  .strict();

/** Market filter: non-empty symbol list, positive threshold, positive window. */
export const marketFiltersSchema = z
  .object({
    symbols: z.array(z.string().min(1).max(10)).min(1, "Add at least one symbol"),
    percentThreshold: z
      .number()
      .positive("Threshold must be positive")
      .max(100, "Threshold must be 100% or less"),
    windowMinutes: z
      .number()
      .int("Window must be a whole number of minutes")
      .positive("Window must be positive")
      .max(24 * 60, "Window must be 24 hours or less"),
  })
  .strict();

/** Disaster filter: regions, event types, severity threshold. */
export const disasterFiltersSchema = z
  .object({
    regions: z
      .array(z.string().min(2).max(10))
      .min(1, "Add at least one region (or use the 'global' sentinel)"),
    eventTypes: z
      .array(z.enum(["earthquake", "hurricane", "flood"]))
      .min(1, "Add at least one event type"),
    minSeverity: z.number().min(0, "Severity must be 0 or higher"),
  })
  .strict();

export type NewsFilters = z.infer<typeof newsFiltersSchema>;
export type MarketFilters = z.infer<typeof marketFiltersSchema>;
export type DisasterFilters = z.infer<typeof disasterFiltersSchema>;

export type FiltersByType = {
  News: NewsFilters;
  Market: MarketFilters;
  Disaster: DisasterFilters;
};

export const filterSchemaFor = (type: AlertType) => {
  switch (type) {
    case "News":
      return newsFiltersSchema;
    case "Market":
      return marketFiltersSchema;
    case "Disaster":
      return disasterFiltersSchema;
  }
};

export const filterSchemaByType: Record<AlertType, z.ZodTypeAny> = {
  News: newsFiltersSchema,
  Market: marketFiltersSchema,
  Disaster: disasterFiltersSchema,
};

/**
 * Parse the raw <c>Filters</c> JSON string for the given alert
 * type. Returns a field-level error on failure so the form can
 * display it next to the type picker.
 */
export const parseFilters = (type: AlertType, raw: string):
  | { success: true; data: FiltersByType[AlertType] }
  | { success: false; message: string } => {
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch (err) {
    return {
      success: false,
      message: err instanceof Error ? err.message : "Filters is not valid JSON",
    };
  }
  const schema = filterSchemaFor(type);
  const result = schema.safeParse(parsed);
  if (!result.success) {
    const first = result.error.issues[0];
    return {
      success: false,
      message: first ? `${first.path.join(".") || "filters"}: ${first.message}` : "Invalid filters",
    };
  }
  return { success: true, data: result.data as FiltersByType[AlertType] };
};

/**
 * Re-serialize parsed filters to the canonical JSON shape. Keeps
 * property order + null vs absent semantics consistent with the
 * backend's serializer output.
 */
export const canonicalize = (type: AlertType, value: FiltersByType[AlertType]): string => {
  const schema = filterSchemaFor(type);
  return JSON.stringify(schema.parse(value));
};

/** Hint that the type's filter shape is required to be non-empty. */
export const isTypeFilterEmpty = (type: AlertType, value: FiltersByType[AlertType]): boolean => {
  switch (type) {
    case "News":
      return false;
    case "Market":
      return (value as MarketFilters).symbols.length === 0;
    case "Disaster":
      return (
        (value as DisasterFilters).regions.length === 0 ||
        (value as DisasterFilters).eventTypes.length === 0
      );
  }
};
