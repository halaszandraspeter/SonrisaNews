"use client";

/**
 * Type-specific filter fields for the alert editor. Renders the
 * right sub-form for the selected alert type. Each sub-form is a
 * controlled form backed by the parsed-typed-filters object; the
 * dialog re-serializes to canonical JSON on submit.
 *
 * No React Hook Form here. The dialog owns the filter state as a
 * single string and parses it for the sub-form to edit. Keeps the
 * state shape simple: a single <c>filtersJson</c> string.
 */

import Box from "@mui/material/Box";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";

import {
  canonicalize,
  disasterFiltersSchema,
  marketFiltersSchema,
  newsFiltersSchema,
  type DisasterFilters,
  type MarketFilters,
  type NewsFilters,
} from "./filterSchemas";

export type AlertFilterFieldsProps = {
  type: "News" | "Market" | "Disaster";
  filtersJson: string;
  onFiltersJsonChange: (next: string) => void;
};

export function AlertFilterFields({
  type,
  filtersJson,
  onFiltersJsonChange,
}: AlertFilterFieldsProps) {
  if (type === "News") {
    return (
      <NewsFilterFields filtersJson={filtersJson} onFiltersJsonChange={onFiltersJsonChange} />
    );
  }
  if (type === "Market") {
    return (
      <MarketFilterFields filtersJson={filtersJson} onFiltersJsonChange={onFiltersJsonChange} />
    );
  }
  return (
    <DisasterFilterFields filtersJson={filtersJson} onFiltersJsonChange={onFiltersJsonChange} />
  );
}

function parse<T>(schema: { safeParse: (v: unknown) => { success: boolean; data?: T } }, json: string): T | null {
  try {
    const raw = JSON.parse(json);
    const result = schema.safeParse(raw);
    return result.success ? (result.data as T) : null;
  } catch {
    return null;
  }
}

function updateFilters<T>(currentJson: string, next: T): string {
  return JSON.stringify(next);
}

function NewsFilterFields({
  filtersJson,
  onFiltersJsonChange,
}: {
  filtersJson: string;
  onFiltersJsonChange: (next: string) => void;
}) {
  const value: NewsFilters = parse(newsFiltersSchema, filtersJson) ?? {
    sourceIds: [],
    keyword: null,
    matchMode: "All",
    tags: [],
  };
  const update = (patch: Partial<NewsFilters>) => onFiltersJsonChange(updateFilters(filtersJson, { ...value, ...patch }));

  return (
    <Stack spacing={2}>
      <Typography variant="subtitle2">News filter</Typography>
      <TextField
        label="Keyword (optional)"
        placeholder="e.g. earthquake"
        value={value.keyword ?? ""}
        onChange={(e) => update({ keyword: e.target.value.length === 0 ? null : e.target.value })}
        fullWidth
      />
      <TextField
        label="Tags (comma-separated, optional)"
        placeholder="e.g. breaking, politics"
        value={value.tags.join(", ")}
        onChange={(e) =>
          update({
            tags: e.target.value
              .split(",")
              .map((t) => t.trim())
              .filter((t) => t.length > 0),
          })
        }
        fullWidth
      />
      <Box>
        <Typography variant="caption" color="text.secondary">
          Leave both blank to match every news event from every enabled source.
        </Typography>
      </Box>
    </Stack>
  );
}

function MarketFilterFields({
  filtersJson,
  onFiltersJsonChange,
}: {
  filtersJson: string;
  onFiltersJsonChange: (next: string) => void;
}) {
  const value: MarketFilters = parse(marketFiltersSchema, filtersJson) ?? {
    symbols: [],
    percentThreshold: 5,
    windowMinutes: 60,
  };
  const update = (patch: Partial<MarketFilters>) =>
    onFiltersJsonChange(updateFilters(filtersJson, { ...value, ...patch }));

  return (
    <Stack spacing={2}>
      <Typography variant="subtitle2">Market filter</Typography>
      <TextField
        label="Symbols (comma-separated)"
        placeholder="e.g. AAPL, MSFT"
        value={value.symbols.join(", ")}
        onChange={(e) =>
          update({
            symbols: e.target.value
              .split(",")
              .map((s) => s.trim().toUpperCase())
              .filter((s) => s.length > 0),
          })
        }
        required
        fullWidth
      />
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          label="Threshold (% change)"
          type="number"
          slotProps={{ htmlInput: { min: 0.1, step: 0.1, max: 100 } }}
          value={value.percentThreshold}
          onChange={(e) => update({ percentThreshold: Number(e.target.value) })}
          required
          fullWidth
        />
        <TextField
          label="Window (minutes)"
          type="number"
          slotProps={{ htmlInput: { min: 1, step: 1, max: 24 * 60 } }}
          value={value.windowMinutes}
          onChange={(e) => update({ windowMinutes: Number(e.target.value) })}
          required
          fullWidth
        />
      </Stack>
      <Typography variant="caption" color="text.secondary">
        Triggers when the absolute % change of any listed symbol over the window is
        at or above the threshold. Data is delayed up to 15 minutes.
      </Typography>
    </Stack>
  );
}

function DisasterFilterFields({
  filtersJson,
  onFiltersJsonChange,
}: {
  filtersJson: string;
  onFiltersJsonChange: (next: string) => void;
}) {
  const value: DisasterFilters = parse(disasterFiltersSchema, filtersJson) ?? {
    regions: [],
    eventTypes: [],
    minSeverity: 5,
  };
  const update = (patch: Partial<DisasterFilters>) =>
    onFiltersJsonChange(updateFilters(filtersJson, { ...value, ...patch }));

  return (
    <Stack spacing={2}>
      <Typography variant="subtitle2">Disaster filter</Typography>
      <TextField
        label="Regions (comma-separated country or US-state codes)"
        placeholder="e.g. JP, US-CA, global"
        value={value.regions.join(", ")}
        onChange={(e) =>
          update({
            regions: e.target.value
              .split(",")
              .map((r) => r.trim().toUpperCase())
              .filter((r) => r.length > 0),
          })
        }
        required
        fullWidth
      />
      <TextField
        label="Event types (comma-separated)"
        placeholder="e.g. earthquake, hurricane, flood"
        value={value.eventTypes.join(", ")}
        onChange={(e) =>
          update({
            eventTypes: e.target.value
              .split(",")
              .map((t) => t.trim().toLowerCase())
              .filter((t) => t.length > 0) as DisasterFilters["eventTypes"],
          })
        }
        required
        fullWidth
      />
      <TextField
        label="Minimum severity"
        type="number"
        slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
        value={value.minSeverity}
        onChange={(e) => update({ minSeverity: Number(e.target.value) })}
        required
        fullWidth
      />
      <Typography variant="caption" color="text.secondary">
        Severity scale is per event type (Richter for earthquakes, Saffir-Simpson
        for hurricanes, etc.).
      </Typography>
    </Stack>
  );
}
