"use client";

/**
 * Client-side leaf that opens the alert editor in create mode.
 * The editor is owned by <c>AlertsEditorHost</c> (a parent of
 * this button) — this button just calls <c>openForCreate</c>
 * on the host. Mounting this button outside the host throws
 * (the <c>useAlertEditorHost</c> hook enforces it).
 */

import Button from "@mui/material/Button";

import { useAlertEditorHost } from "./AlertsEditorHost";

export type NewAlertButtonProps = {
  label?: string;
};

export function NewAlertButton({ label = "New alert" }: NewAlertButtonProps) {
  const { openForCreate } = useAlertEditorHost();
  return (
    <Button variant="contained" onClick={openForCreate}>
      {label}
    </Button>
  );
}
