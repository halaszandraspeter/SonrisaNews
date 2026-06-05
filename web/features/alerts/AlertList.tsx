"use client";

/**
 * Alert list. Server data from <c>useAlertsQuery</c>, grouped by
 * type. Each row is an <c>AlertCard</c> with the edit, delete, and
 * "test this alert" actions. Loading / error / empty states are
 * explicit. No spinners.
 *
 * The editor dialog is owned by <c>AlertsEditorHost</c> (a parent
 * of this list); each card's Edit action calls
 * <c>openForEdit(alert)</c> on the host. Mounting this list
 * outside the host throws (the <c>useAlertEditorHost</c> hook
 * enforces it).
 */

import { useMemo } from "react";
import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardActions from "@mui/material/CardActions";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Skeleton from "@mui/material/Skeleton";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import Typography from "@mui/material/Typography";

import Link from "next/link";

import { useAlertEditorHost } from "./AlertsEditorHost";
import { TestAlertButton } from "./TestAlertButton";
import { alertTypeLabel, alertTypeOrder, type AlertType } from "./alertTypes";
import {
  useAlertsQuery,
  useDeleteAlertMutation,
  useUpdateAlertMutation,
} from "./useAlertsQueries";
import type { AlertResponse } from "@/lib/api/schema";

const SKELETON_ROWS = 3;

export function AlertList() {
  const { data, isPending, isError, error, refetch } = useAlertsQuery();

  const groups = useMemo(() => groupByType(data ?? []), [data]);

  if (isPending) {
    return (
      <Stack spacing={2} aria-busy="true">
        {Array.from({ length: SKELETON_ROWS }).map((_, i) => (
          <Skeleton key={i} variant="rectangular" height={120} />
        ))}
      </Stack>
    );
  }

  if (isError) {
    return (
      <Alert
        severity="error"
        action={
          <Button color="inherit" size="small" onClick={() => void refetch()}>
            Retry
          </Button>
        }
      >
        <AlertTitle>Couldn&apos;t load your alerts</AlertTitle>
        {error.message}
      </Alert>
    );
  }

  if (data === undefined || data.length === 0) {
    // The empty state is a single card. The "New alert" button
    // is in the page header (rendered by the host); we don't
    // need a duplicate here.
    return <EmptyState />;
  }

  return (
    <Stack spacing={4}>
      {alertTypeOrder.map((type) => {
        const rows = groups[type];
        if (rows.length === 0) return null;
        return (
          <Stack key={type} spacing={1.5}>
            <Typography variant="h6" component="h2">
              {alertTypeLabel[type]}
            </Typography>
            <Stack spacing={1.5}>
              {rows.map((alert) => (
                <AlertCard key={alert.Id} alert={alert} />
              ))}
            </Stack>
          </Stack>
        );
      })}
    </Stack>
  );
}

/**
 * Group alerts by type. A <c>Record</c> (not a <c>Map</c>) so
 * TypeScript narrows correctly on <c>groups[type]</c> without
 * the <c>.get(type) ?? []</c> dance.
 */
function groupByType(alerts: ReadonlyArray<AlertResponse>): Record<AlertType, AlertResponse[]> {
  const out: Record<AlertType, AlertResponse[]> = {
    News: [],
    Market: [],
    Disaster: [],
  };
  for (const alert of alerts) {
    out[alert.Type].push(alert);
  }
  return out;
}

function EmptyState() {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={1.5} sx={{ alignItems: "flex-start" }}>
          <Typography variant="h6" component="h2">
            No alerts yet
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Use the <strong>New alert</strong> button above to create your first
            one. Alerts are inert until the matcher wires them up in wave 6, so
            you can experiment freely.
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}

function AlertCard({ alert }: { alert: AlertResponse }) {
  const { openForEdit } = useAlertEditorHost();
  const deleteMutation = useDeleteAlertMutation();
  const updateMutation = useUpdateAlertMutation(alert.Id);

  const handleDelete = () => {
    if (typeof window !== "undefined") {
      const ok = window.confirm(`Delete alert "${alert.Name}"? This can't be undone.`);
      if (!ok) return;
    }
    deleteMutation.mutate(alert.Id);
  };

  const handleToggleEnabled = (_: unknown, checked: boolean) => {
    updateMutation.mutate({ Enabled: checked });
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: "flex-start" }}>
          <Stack spacing={0.5} sx={{ flexGrow: 1 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <Typography
                variant="subtitle1"
                component={Link}
                href={`/alerts/${alert.Id}`}
                sx={{
                  color: "primary.main",
                  textDecoration: "none",
                  "&:hover": { textDecoration: "underline" },
                }}
              >
                {alert.Name}
              </Typography>
              <Chip size="small" label={alertTypeLabel[alert.Type]} />
            </Stack>
            <FilterSummary alert={alert} />
          </Stack>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Typography variant="caption" color="text.secondary">
              {alert.Enabled ? "Enabled" : "Disabled"}
            </Typography>
            <Switch
              checked={alert.Enabled}
              onChange={handleToggleEnabled}
              disabled={updateMutation.isPending}
              slotProps={{ input: { "aria-label": `Enable ${alert.Name}` } }}
            />
          </Stack>
        </Stack>
      </CardContent>
      <CardActions sx={{ px: 2, pb: 2, pt: 0 }}>
        <Button size="small" onClick={() => openForEdit(alert)}>
          Edit
        </Button>
        <TestAlertButton alertId={alert.Id} alertName={alert.Name} />
        <Box sx={{ flexGrow: 1 }} />
        <Button
          size="small"
          color="error"
          onClick={handleDelete}
          disabled={deleteMutation.isPending}
        >
          Delete
        </Button>
      </CardActions>
    </Card>
  );
}

function FilterSummary({ alert }: { alert: AlertResponse }) {
  // The backend stores the per-type filter shape as a JSON
  // string. Render a one-liner summary so the card stays scannable.
  let summary = "No filter set";
  try {
    const parsed = JSON.parse(alert.Filters) as Record<string, unknown>;
    switch (alert.Type) {
      case "News": {
        const keyword = typeof parsed["keyword"] === "string" ? parsed["keyword"] : null;
        const tags = Array.isArray(parsed["tags"]) ? (parsed["tags"] as string[]) : [];
        const parts: string[] = [];
        if (keyword) parts.push(`keyword: "${keyword}"`);
        if (tags.length > 0) parts.push(`tags: ${tags.join(", ")}`);
        summary = parts.length === 0 ? "All news" : parts.join(" · ");
        break;
      }
      case "Market": {
        const symbols = Array.isArray(parsed["symbols"]) ? (parsed["symbols"] as string[]) : [];
        const threshold = parsed["percentThreshold"];
        const window = parsed["windowMinutes"];
        summary = `${symbols.join(", ") || "—"} · ±${String(threshold)}% in ${String(window)}m`;
        break;
      }
      case "Disaster": {
        const regions = Array.isArray(parsed["regions"]) ? (parsed["regions"] as string[]) : [];
        const eventTypes = Array.isArray(parsed["eventTypes"])
          ? (parsed["eventTypes"] as string[])
          : [];
        const minSeverity = parsed["minSeverity"];
        summary = `${eventTypes.join(", ") || "—"} in ${regions.join(", ") || "—"} ≥ ${String(minSeverity)}`;
        break;
      }
    }
  } catch {
    // Filters JSON is malformed; leave the default summary.
  }
  return (
    <Typography variant="body2" color="text.secondary">
      {summary}
    </Typography>
  );
}
