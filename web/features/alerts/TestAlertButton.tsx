"use client";

/**
 * "Test this alert" button. Client-side leaf that owns the
 * dialog open-state. The dialog re-runs the matcher against
 * the most recent 50 events and shows the would-have-fired
 * list. The matcher is read-only — no <c>Match</c> or
 * <c>Notification</c> rows are written, so the user can poke
 * at the button freely.
 */

import { useState } from "react";
import Button from "@mui/material/Button";

import { TestAlertDialog } from "./TestAlertDialog";

export type TestAlertButtonProps = {
  alertId: string;
  alertName: string;
};

export function TestAlertButton({ alertId, alertName }: TestAlertButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button size="small" onClick={() => setOpen(true)} aria-label={`Test ${alertName}`}>
        Test
      </Button>
      <TestAlertDialog
        open={open}
        onClose={() => setOpen(false)}
        alertId={alertId}
        alertName={alertName}
      />
    </>
  );
}
