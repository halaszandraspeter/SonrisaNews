import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import { AlertsPageBody } from "@/features/alerts/AlertsPageBody";

/**
 * The post-sign-in dashboard. Renders the alert list (grouped
 * by type, with edit / delete / test actions), the page-header
 * "New alert" + "Add channel" buttons, and the channel-mode
 * matrix link on the per-alert detail page.
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
            Manage your alerts and the channels they deliver to. Alerts are
            matched against polled events every two minutes; click an
            alert&apos;s name to open the channel-mode matrix, or hit{" "}
            <strong>Test</strong> to preview what would fire right now.
          </Typography>
        </Stack>
        <Alert severity="info">
          <AlertTitle>Wave 6 — news poller + matcher</AlertTitle>
          Alerts now run against polled news events. Create, edit, enable, and
          delete alerts. The <strong>Test</strong> button previews matches
          against the last 50 events without firing any notifications.
        </Alert>
        <AlertsPageBody />
      </Stack>
    </Container>
  );
}
