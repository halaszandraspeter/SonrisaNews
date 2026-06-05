"use client";

import { useState } from "react";
import Button from "@mui/material/Button";
import { AddChannelDialog } from "@/features/channels/AddChannelDialog";

interface AddChannelButtonProps {
  label?: string;
  /**
   * Called after the channel is successfully verified. The
   * default behavior is to close the dialog. Pass an override
   * to also refresh downstream queries (e.g. the matrix's
   * channel list). See <c>AddChannelButtonWithRefresh</c>.
   */
  onChannelCreated?: (channelId: string) => void;
}

/**
 * Client-side leaf that owns the open/close state of the
 * <c>AddChannelDialog</c>. The dialog reports the new
 * channel's id back via <c>onChannelCreated</c>; the host
 * page can use it to invalidate the <c>["channels"]</c>
 * query.
 */
export function AddChannelButton({
  label = "Add Channel",
  onChannelCreated,
}: AddChannelButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="contained" onClick={() => setOpen(true)}>
        {label}
      </Button>
      <AddChannelDialog
        open={open}
        onClose={() => setOpen(false)}
        onChannelCreated={(channelId) => {
          onChannelCreated?.(channelId);
          setOpen(false);
        }}
      />
    </>
  );
}
