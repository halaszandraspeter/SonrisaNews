"use client";

/**
 * Channel-mode matrix. Rows are alerts, columns are channels,
 * cells are the delivery mode dropdown. Rendering: a CSS grid
 * with a header row of channel destinations; each data row is an
 * alert (name + type) followed by one dropdown per channel.
 *
 * The matrix is a controlled component over the
 * <c>AlertChannelModeResponse</c> rows for the current alert. The
 * parent supplies:
 *   - <c>channels</c>: the caller's channel list (for the column
 *     headers)
 *   - <c>modes</c>: the current channel-mode rows for this alert
 *   - <c>onChange</c>: called with a (channelId, mode | null) pair
 *     whenever the user picks a different mode or clears a cell.
 *
 * The <c>Set</c> (or <c>Remove</c>) is delegated to the parent so
 * the matrix doesn't own the React Query mutation. This keeps the
 * matrix testable as a pure renderer + onChange callback.
 */

import Box from "@mui/material/Box";
import FormControl from "@mui/material/FormControl";
import MenuItem from "@mui/material/MenuItem";
import Select from "@mui/material/Select";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import {
  DELIVERY_MODES,
  deliveryModeHint,
  deliveryModeLabel,
  type DeliveryMode,
} from "./deliveryModes";
import type {
  AlertChannelModeResponse,
  AlertResponse,
  ChannelResponse,
} from "@/lib/api/schema";

export type ChannelModeMatrixProps = {
  alert: AlertResponse;
  channels: ReadonlyArray<ChannelResponse>;
  modes: ReadonlyArray<AlertChannelModeResponse>;
  onChange: (channelId: string, mode: DeliveryMode | null) => void;
  isUpdating: boolean;
};

/**
 * Per-cell value the <Select> renders. Three states:
 *   - a <c>DeliveryMode</c> string — the row exists and is set;
 *   - <c>""</c> — the user explicitly picked "Don't deliver";
 *   - <c>undefined</c> — no row exists for this
 *     <c>(alertId, channelId)</c> pair (the dropdown shows the
 *     em-dash placeholder).
 *
 * The three states matter because the matrix only fires
 * <c>onChange(channelId, null)</c> when the previous value was a
 * real mode (so the parent calls <c>removeChannelMode</c>); an
 * explicit "Don't deliver" picked from a missing-row state is a
 * no-op. Without the distinction, a user who opens the dropdown
 * and picks the same option they already have would round-trip a
 * redundant DELETE.
 */
type CellValue = DeliveryMode | "" | undefined;

export function ChannelModeMatrix({
  alert,
  channels,
  modes,
  onChange,
  isUpdating,
}: ChannelModeMatrixProps) {
  const valueForChannel = (channelId: string): CellValue => {
    const row = modes.find((m) => m.ChannelId === channelId);
    return row?.Mode ?? undefined;
  };

  return (
    <Box
      role="group"
      aria-label={`Delivery modes for ${alert.Name}`}
      sx={{
        display: "grid",
        gridTemplateColumns: `minmax(160px, 1fr) repeat(${channels.length}, minmax(140px, 1fr))`,
        gap: 1,
        alignItems: "center",
      }}
    >
      <Typography variant="subtitle2" sx={{ pl: 1 }}>
        Channel
      </Typography>
      {channels.map((channel) => (
        <ChannelHeader key={channel.Id} channel={channel} />
      ))}

      <Typography variant="body2" sx={{ pl: 1 }}>
        {alert.Name}
      </Typography>
      {channels.map((channel) => {
        const previous = valueForChannel(channel.Id);
        return (
          <FormControl key={channel.Id} size="small" fullWidth>
            <Select
              value={previous ?? ""}
              displayEmpty
              disabled={isUpdating}
              onChange={(e) => {
                const next = e.target.value as DeliveryMode | "";
                if (next === "") {
                  // Only treat as "remove" when there was
                  // something to remove. The previous === ""
                  // branch is the no-op (user picked "Don't
                  // deliver" on an already-cleared cell); the
                  // previous === undefined branch is also a
                  // no-op (nothing in the DB yet).
                  if (previous !== "" && previous !== undefined) {
                    onChange(channel.Id, null);
                  }
                } else {
                  onChange(channel.Id, next);
                }
              }}
              renderValue={(selected) => {
                const value = selected as DeliveryMode | "" | undefined;
                if (value === "" || value === undefined) {
                  return (
                    <Typography variant="body2" color="text.secondary">
                      —
                    </Typography>
                  );
                }
                return deliveryModeLabel[value];
              }}
              slotProps={{
                input: {
                  "aria-label": `Delivery mode for ${alert.Name} on ${channel.Destination}`,
                },
              }}
            >
              <MenuItem value="">
                <em>Don&apos;t deliver</em>
              </MenuItem>
              {DELIVERY_MODES.map((mode) => (
                <MenuItem key={mode} value={mode}>
                  <Stack>
                    <Typography variant="body2">{deliveryModeLabel[mode]}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {deliveryModeHint[mode]}
                    </Typography>
                  </Stack>
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        );
      })}
    </Box>
  );
}

function ChannelHeader({ channel }: { channel: ChannelResponse }) {
  return (
    <Box sx={{ px: 1 }}>
      <Typography variant="caption" color="text.secondary">
        {channel.Type}
      </Typography>
      <Typography variant="body2" noWrap title={channel.Destination}>
        {channel.Destination}
      </Typography>
      {!channel.Verified ? (
        <Typography variant="caption" color="warning.main">
          Unverified
        </Typography>
      ) : null}
    </Box>
  );
}
