"use client";

import { useEffect } from "react";

import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";

/**
 * Error boundary for the <c>(auth)</c> route group. Catches any
 * uncaught render error in the auth pages. The user gets a
 * "Something went wrong" message with a retry button.
 */
export default function AuthError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    // Wire to a real logger (Sentry, etc.) in wave 11+; for now
    // console.error is the only signal in dev.
    console.error("(auth) route error", error);
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
