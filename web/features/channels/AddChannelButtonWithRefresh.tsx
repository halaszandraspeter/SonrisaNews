"use client";

/**
 * Add-channel button that invalidates the <c>["channels"]</c>
 * query after a successful create. The plain
 * <c>AddChannelButton</c> leaf closes the dialog on success but
 * doesn't refresh the channel list. When the alert detail
 * page hosts the channel-mode matrix, the matrix's column
 * headers would go stale without an explicit invalidation.
 *
 * Use this wrapper on any page that renders the matrix. The
 * regular <c>AddChannelButton</c> is fine for pages that don't
 * read the channels list (e.g. a future dedicated channels
 * management page that already has its own invalidation).
 *
 * The wrapper is an <em>invalidator</em>, not an
 * optimistic-prepend hook. A future caller that wants to
 * optimistically prepend the new channel to the query cache
 * should write a separate wrapper that <em>uses</em> the
 * <c>channelId</c> argument.
 */

import { useQueryClient } from "@tanstack/react-query";

import { AddChannelButton } from "@/features/channels/AddChannelButton";
import { channelsQueryKey } from "@/lib/api/keys";

export function AddChannelButtonWithRefresh({ label }: { label?: string }) {
  const qc = useQueryClient();
  return (
    <AddChannelButton
      label={label}
      onChannelCreated={(_channelId) => {
        void qc.invalidateQueries({ queryKey: channelsQueryKey });
      }}
    />
  );
}
