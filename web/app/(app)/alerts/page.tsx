import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import { AlertsPageBody } from "@/features/alerts/AlertsPageBody";

/**
 * The post-sign-in dashboard. Wave 5 ships the full alert list
 * (grouped by type, with edit/delete/test actions) plus the
 * "add channel" + "new alert" buttons in the page header. The
 * channel-mode matrix lives at <c>/alerts/{id}</c>.
 *
 * Server Component — the auth gate runs in the parent
 * <c>(app)/layout.tsx</c> via <c>AuthGate</c>. The interactive
 * body (list, buttons, editor dialog) lives in
 * <c>AlertsPageBody</c> so the host can be a single client tree.
 */
export default function AlertsPage() {
  return (
    <Container maxWidth="md" component="main">
      <Stack spacing={4} sx={{ py: { xs: 6, md: 10 } }}>
        <Stack spacing={1}>
          <Typography variant="h4" component="h1">
            Your alerts
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Manage your alerts and the channels they deliver to. Alerts are inert
            until the matcher wires them up in wave 6.
          </Typography>
        </Stack>
        <Alert severity="info">
          <AlertTitle>Wave 5 — alert CRUD + filters</AlertTitle>
          Create, edit, enable, and delete alerts. Click an alert&apos;s row to
          open the channel-mode matrix.
        </Alert>
        <AlertsPageBody />
      </Stack>
    </Container>
  );
}
