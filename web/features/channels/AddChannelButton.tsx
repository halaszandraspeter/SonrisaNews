"use client";

import { useState } from "react";
import Button from "@mui/material/Button";
import { AddChannelDialog } from "@/features/channels/AddChannelDialog";

interface AddChannelButtonProps {
  label?: string;
}

/**
 * Client-side leaf that owns the open/close state of the AddChannelDialog.
 * Server-rendered pages stay server components; only this button + the
 * dialog itself are client-side (they need state, event handlers, and
 * the React Query mutations).
 */
export function AddChannelButton({ label = "Add Channel" }: AddChannelButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="contained" onClick={() => setOpen(true)}>
        {label}
      </Button>
      <AddChannelDialog
        open={open}
        onClose={() => setOpen(false)}
        onChannelCreated={() => {
          setOpen(false);
        }}
      />
    </>
  );
}
