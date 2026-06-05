"use client";

import { useEffect } from "react";

import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";

/**
 * Error boundary for the <c>(app)</c> route group. Catches any
 * uncaught render error in the dashboard pages. Wire to a real
 * logger (Sentry, etc.) in wave 11+; for now <c>console.error</c>
 * is the only signal in dev.
 */
export default function AppError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("(app) route error", error);
  }, [error]);

  return (
    <Stack spacing={2} sx={{ p: 4 }}>
      <Alert severity="error">
        <AlertTitle>Something went wrong</AlertTitle>
        {error.message || "An unexpected error occurred. Please try again."}
      </Alert>
      <Button onClick={reset} variant="contained">
        Try again
      </Button>
    </Stack>
  );
}
