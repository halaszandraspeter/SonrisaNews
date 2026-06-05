"use client";

/**
 * "Test this alert" dialog. Re-runs the matcher against the most
 * recent 50 events for the alert and shows the would-have-fired
 * list. Wired to <c>POST /api/v1/alerts/{id}/test</c>.
 *
 * The matcher is a read-only preview: the backend inserts no
 * <c>Match</c> or <c>Notification</c> rows, so the user can poke
 * at the dialog as often as they like without polluting the
 * activity feed. The "Run again" button re-fires the request
 * (useful when sources have polled and new events have arrived
 * since the dialog opened).
 *
 * Loading, error, empty, and results states are all explicit.
 * The dialog title shows the alert name; the results list shows
 * the would-have-fired summaries (one per matched event). A
 * single primary action ("Close") dismisses the dialog.
 *
 * Concurrency: the "Run again" handler awaits the in-flight
 * promise (via <c>mutateAsync</c>) before re-firing. React
 * Query has no built-in cancel for fire-and-forget mutations,
 * so an unguarded double-click would fire two requests. The
 * <c>isPending</c> guard is belt + suspenders — the button
 * is also <c>disabled={mutation.isPending}</c>.
 */

import { useEffect } from "react";
import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
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

import { useTestAlertMutation } from "./useAlertsQueries";
import type { TestAlertHit } from "@/lib/api/schema";

type TestAlertDialogProps = {
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

  // Fire the test on the open transition AND when the alert
  // changes (e.g. the user closes the dialog on alert A and
  // opens it on alert B without unmounting the dialog).
  // We deliberately depend on `open` and `alertId` only.
  // Re-running the mutation on a re-render of the dialog
  // body would round-trip a request for no reason.
  // The hook is keyed on `alertId` so a `alertId` change
  // creates a fresh mutation instance; resetting the prior
  // one and starting the new one is the right cadence.
  // If a flash of stale content during an `alertId`
  // transition is unacceptable, the parent should re-mount
  // the dialog with `key={alertId}`.
  useEffect(() => {
    if (!open) return;
    mutation.reset();
    mutation.mutate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, alertId]);

  const runAgain = async () => {
    // Belt + suspenders: the button is disabled while
    // in-flight, but a programmatic invocation (or a
    // keyboard + state-transition race) could still land
    // a second click. Awaiting the prior promise makes the
    // operation safe to re-trigger.
    if (mutation.isPending) return;
    mutation.reset();
    try {
      await mutation.mutateAsync();
    } catch {
      // The error is already reflected via `mutation.isError`
      // in the rendered view; the `await` is here for the
      // sequencing guarantee, not for surfacing the error.
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      aria-label={`Test alert ${alertName}`}
    >
      <DialogTitle>Test &quot;{alertName}&quot;</DialogTitle>
      <DialogContent dividers>
        {mutation.isPending ? (
          <Stack sx={{ py: 4, alignItems: "center" }} aria-busy="true">
            <CircularProgress />
            <Typography variant="body2" sx={{ mt: 2 }}>
              Running the matcher against the last 50 events…
            </Typography>
          </Stack>
        ) : mutation.isError ? (
          <Alert severity="error">
            <AlertTitle>Test failed</AlertTitle>
            {mutation.error?.message ?? "Unknown error"}
          </Alert>
        ) : mutation.data !== undefined ? (
          <Results hits={mutation.data} />
        ) : null}
      </DialogContent>
      <DialogActions>
        <Button onClick={runAgain} disabled={mutation.isPending}>
          Run again
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        <Button onClick={onClose} variant="contained" autoFocus>
          Close
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function Results({ hits }: { hits: ReadonlyArray<TestAlertHit> }) {
  if (hits.length === 0) {
    return (
      <Stack spacing={1}>
        <Typography variant="body2" color="text.secondary">
          No events in the last 50 would have fired this alert.
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Polling has to have fetched at least one event for this alert&apos;s
          type. The matcher is read-only — no <code>Match</code> rows are written.
        </Typography>
      </Stack>
    );
  }

  return (
    <Stack spacing={2}>
      <Typography variant="body2" color="text.secondary">
        {hits.length === 1
          ? "1 event would have fired this alert."
          : `${hits.length} events would have fired this alert.`}
      </Typography>
      <List dense aria-label="Would-have-fired events">
        {hits.map((hit) => (
          <ListItem key={hit.EventId} alignItems="flex-start">
            <ListItemText
              primary={<HitSummary summary={hit.Summary} />}
              secondary={
                <Typography component="span" variant="caption" color="text.secondary">
                  Event {hit.EventId.slice(0, 8)}
                </Typography>
              }
            />
          </ListItem>
        ))}
      </List>
    </Stack>
  );
}

/**
 * Renders a hit's summary. An empty summary means the event's
 * payload had no extractable text (the matcher's <c>Summarize</c>
 * returned an empty string). Surface this as a distinct "empty
 * payload" chip so the user can tell a 50-item "no summary" list
 * from a 50-item list with real content — and so they know it's
 * a data-source problem, not a matcher problem.
 */
function HitSummary({ summary }: { summary: string }) {
  if (summary.trim().length === 0) {
    return (
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <Chip size="small" label="empty payload" />
        <Typography component="span" variant="caption" color="text.secondary">
          the matched event had no extractable summary
        </Typography>
      </Stack>
    );
  }
  return summary;
}
