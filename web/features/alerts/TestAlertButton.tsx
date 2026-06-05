"use client";

/**
 * "Test this alert" button. Client-side leaf that owns the test
 * dialog open-state. The matcher lands in wave 6; the button is
 * wired now so the test can be re-run as soon as wave 6 lands.
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
