"use client";

/**
 * "Test this alert" dialog. Re-runs the matcher against the most
 * recent 50 events for the alert and shows the would-have-fired
 * list. The matcher lands in wave 6; until then the backend
 * returns 501 and the dialog shows a "wired in wave 6" message
 * (the test mutation throws <c>MatcherNotWiredError</c> which we
 * catch here).
 */

import { useEffect, useState } from "react";
import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import { MatcherNotWiredError, useTestAlertMutation } from "./useAlertsQueries";
import type { AlertTestResponse } from "@/lib/api/schema";

export type TestAlertDialogProps = {
  open: boolean;
  onClose: () => void;
  alertId: string;
  alertName: string;
};

export function TestAlertDialog({
  open,
  onClose,
  alertId,
  alertName,
}: TestAlertDialogProps) {
  const mutation = useTestAlertMutation(alertId);
  const [result, setResult] = useState<AlertTestResponse | null>(null);
  const [wiringPending, setWiringPending] = useState(false);

  // Run the test as soon as the dialog opens.
  useEffect(() => {
    if (!open) return;
    setResult(null);
    setWiringPending(false);
    mutation.mutate(undefined, {
      onSuccess: (data) => setResult(data),
      onError: (err) => {
        if (err instanceof MatcherNotWiredError) {
          setWiringPending(true);
        }
      },
    });
    // We deliberately depend on `open` only — running the test
    // once per open is the right cadence. Re-running mid-dialog
    // would re-trigger a request on every state update.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Test &quot;{alertName}&quot;</DialogTitle>
      <DialogContent>
        {wiringPending ? (
          <Alert severity="info">
            <AlertTitle>Matcher not yet wired</AlertTitle>
            The matcher lands in wave 6. Once it ships, this button re-runs the
            matcher against the most recent 50 events and shows you which
            events <em>would have</em> fired this alert.
          </Alert>
        ) : mutation.isPending ? (
          <Stack sx={{ py: 4, alignItems: "center" }} aria-busy="true">
            <CircularProgress />
            <Typography variant="body2" sx={{ mt: 2 }}>
              Running the matcher against the last 50 events…
            </Typography>
          </Stack>
        ) : mutation.isError && !wiringPending ? (
          <Alert severity="error">
            <AlertTitle>Test failed</AlertTitle>
            {mutation.error?.message ?? "Unknown error"}
          </Alert>
        ) : result !== null ? (
          <Stack spacing={2}>
            <Typography variant="body2" color="text.secondary">
              {result.MatchedCount === 0
                ? "No events in the last 50 would have fired this alert."
                : `${result.MatchedCount} event${result.MatchedCount === 1 ? "" : "s"} would have fired this alert.`}
            </Typography>
            {result.Matches.length > 0 ? (
              <List dense>
                {result.Matches.map((m) => (
                  <ListItem key={m.EventId} alignItems="flex-start">
                    <ListItemText
                      primary={m.Title}
                      secondary={
                        <Box component="span">
                          <Typography component="span" variant="caption" color="text.secondary">
                            {m.Source} · {new Date(m.OccurredAt).toLocaleString()}
                          </Typography>
                          <Typography component="p" variant="body2" sx={{ mt: 0.5 }}>
                            {m.Snippet}
                          </Typography>
                        </Box>
                      }
                    />
                  </ListItem>
                ))}
              </List>
            ) : null}
          </Stack>
        ) : null}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
