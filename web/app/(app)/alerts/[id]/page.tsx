"use client";

/**
 * Per-alert detail page (client component). Uses the React
 * Query hooks to fetch the alert, the user's channels, and the
 * channel-mode rows; renders the channel-mode matrix.
 *
 * Why client-side: the auth design (wave 3) keeps the access
 * token in memory only. A Server Component that calls the
 * OpenAPI client would have no token to attach. A future wave
 * (11+) will introduce a short-lived server-readable cookie
 * that lets Server Components call the API directly.
 */

import { use } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Container from "@mui/material/Container";
import Skeleton from "@mui/material/Skeleton";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import Link from "next/link";

import { AlertDetail } from "@/features/alerts/AlertDetail";
import { useAlertsQuery } from "@/features/alerts/useAlertsQueries";

export default function AlertDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { data, isPending, isError, error, refetch } = useAlertsQuery();

  if (isPending) {
    return (
      <Container maxWidth="lg" component="main" aria-busy="true">
        <Stack spacing={2} sx={{ py: 4 }}>
          <Skeleton variant="text" width="20%" />
          <Skeleton variant="rectangular" height={200} />
        </Stack>
      </Container>
    );
  }

  if (isError) {
    return (
      <Container maxWidth="md" component="main">
        <Stack spacing={2} sx={{ py: 6 }}>
          <Alert severity="error" action={<Button onClick={() => void refetch()}>Retry</Button>}>
            Couldn&apos;t load your alerts: {error.message}
          </Alert>
        </Stack>
      </Container>
    );
  }

  const alert = (data ?? []).find((a) => a.Id === id);

  if (alert === undefined) {
    // Either the alert doesn't exist or the caller doesn't own
    // it. Either way, render the not-found view. No existence
    // leak.
    return (
      <Container maxWidth="md" component="main">
        <Stack spacing={2} sx={{ py: 6 }}>
          <Typography variant="h5" component="h1">
            Alert not found
          </Typography>
          <Typography variant="body2" color="text.secondary">
            The alert you tried to reach does not exist or you don&apos;t have
            permission to view it.
          </Typography>
          <Box>
            <Button component={Link} href="/alerts" variant="contained">
              Back to alerts
            </Button>
          </Box>
        </Stack>
      </Container>
    );
  }

  return (
    <Container maxWidth="lg" component="main">
      <Stack spacing={2} sx={{ py: 4 }}>
        <Box>
          <Button component={Link} href="/alerts" variant="text">
            ← All alerts
          </Button>
        </Box>
        <Typography variant="h4" component="h1">
          {alert.Name}
        </Typography>
        <AlertDetail alert={alert} />
      </Stack>
    </Container>
  );
}
