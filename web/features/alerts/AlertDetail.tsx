"use client";

/**
 * Single-alert detail view. Server-fetched via the React Query
 * hooks; renders the channel-mode matrix and the alert metadata.
 * The matrix is the only editable surface on this page; the
 * alert's name / filters are edited via the editor dialog (wave 9
 * dashboard) or the channel editor (parent).
 *
 * The view is intentionally minimal: the wave 5 dashboard at
 * <c>/alerts</c> groups alerts by type and provides the editor
 * affordance from the list row. The detail page is for the
 * matrix and the "test" action.
 */

import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import { AddChannelButtonWithRefresh } from "@/features/channels/AddChannelButtonWithRefresh";
import { ChannelModeMatrix } from "./ChannelModeMatrix";
import { TestAlertButton } from "./TestAlertButton";
import { alertTypeLabel } from "./alertTypes";
import { deliveryModeLabel, type DeliveryMode } from "./deliveryModes";
import {
  useAlertChannelModesQuery,
  useChannelsQuery,
  useRemoveChannelModeMutation,
  useSetChannelModeMutation,
} from "./useAlertsQueries";
import type { AlertResponse, ChannelResponse } from "@/lib/api/schema";

export type AlertDetailProps = {
  alert: AlertResponse;
};

export function AlertDetail({ alert }: AlertDetailProps) {
  const channelsQuery = useChannelsQuery();
  const modesQuery = useAlertChannelModesQuery(alert.Id);
  const setMode = useSetChannelModeMutation(alert.Id);
  const removeMode = useRemoveChannelModeMutation(alert.Id);

  if (channelsQuery.isPending || modesQuery.isPending) {
    return (
      <Stack sx={{ py: 6, alignItems: "center" }} aria-busy="true">
        <CircularProgress />
      </Stack>
    );
  }

  if (channelsQuery.isError) {
    return (
      <Alert severity="error" action={<Button onClick={() => channelsQuery.refetch()}>Retry</Button>}>
        <AlertTitle>Couldn&apos;t load your channels</AlertTitle>
        {channelsQuery.error.message}
      </Alert>
    );
  }

  const channels: ChannelResponse[] = channelsQuery.data ?? [];
  const modes = modesQuery.data ?? [];
  const isUpdating = setMode.isPending || removeMode.isPending;

  return (
    <Container maxWidth="lg" component="section" aria-label={`Alert ${alert.Name}`}>
      <Stack spacing={3} sx={{ py: 4 }}>
        <Stack direction="row" spacing={2} sx={{ alignItems: "center", flexWrap: "wrap" }}>
          <Typography variant="body2" color="text.secondary">
            {alertTypeLabel[alert.Type]} · {alert.Enabled ? "Enabled" : "Disabled"}
          </Typography>
          <Box sx={{ flexGrow: 1 }} />
          <TestAlertButton alertId={alert.Id} alertName={alert.Name} />
          <AddChannelButtonWithRefresh />
        </Stack>

        <Typography variant="h6" component="h2">
          Channel-mode matrix
        </Typography>

        {channels.length === 0 ? (
          <Alert
            severity="info"
            action={<AddChannelButtonWithRefresh label="Add channel" />}
          >
            <AlertTitle>No channels yet</AlertTitle>
            Add a notification channel (email or Slack) to set delivery modes for this
            alert.
          </Alert>
        ) : (
          <Card variant="outlined">
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="subtitle1">Channel × delivery mode</Typography>
                <Typography variant="body2" color="text.secondary">
                  Pick a delivery mode per channel. The matcher routes matches per
                  this matrix.
                </Typography>
                <ChannelModeMatrix
                  alert={alert}
                  channels={channels}
                  modes={modes}
                  isUpdating={isUpdating}
                  onChange={(channelId, mode) => {
                    if (mode === null) {
                      removeMode.mutate(channelId);
                    } else {
                      setMode.mutate({ ChannelId: channelId, Mode: mode });
                    }
                  }}
                />
                {modes.length > 0 ? (
                  <Typography variant="caption" color="text.secondary">
                    Currently set:{" "}
                    {modes
                      .map((m) => {
                        const ch = channels.find((c) => c.Id === m.ChannelId);
                        const label = ch ? `${ch.Type} ${ch.Destination}` : m.ChannelId.slice(0, 8);
                        return `${label} → ${deliveryModeLabel[m.Mode as DeliveryMode]}`;
                      })
                      .join(" · ")}
                  </Typography>
                ) : null}
              </Stack>
            </CardContent>
          </Card>
        )}
      </Stack>
    </Container>
  );
}
