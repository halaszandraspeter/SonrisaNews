import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { AddChannelButton } from "@/features/channels/AddChannelButton";

/**
 * The post-sign-in landing page. The real
 * "alerts dashboard" with grouped lists, "test this alert" buttons,
 * and the channel-mode matrix lands in wave 5. This is the wave 4
 * stub with the Add Channel button + dialog integrated.
 *
 * Server Component — the auth gate runs in the parent
 * <c>(app)/layout.tsx</c> via <c>AuthGate</c>. The AddChannelButton is
 * the only client-side leaf (it owns dialog open/close state and the
 * React Query mutations).
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
            Manage your notification channels and configure alerts. The full
            dashboard with alert list and channel-mode matrix arrives in wave 5.
          </Typography>
        </Stack>
        <Alert severity="info">
          <AlertTitle>Wave 4 development</AlertTitle>
          The Add Channel dialog is now available. Test it by clicking the button
          below. The full dashboard with alert management arrives in wave 5.
        </Alert>
        <Stack direction="row" spacing={2}>
          <AddChannelButton />
        </Stack>
      </Stack>
    </Container>
  );
}

