import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * The post-sign-in landing page. Stub for wave 3 — the real
 * "alerts dashboard" with grouped lists, "test this alert" buttons,
 * and the channel-mode matrix lands in wave 4. The stub exists so
 * the sign-in / sign-up forms' <c>router.push("/alerts")</c> lands
 * somewhere usable instead of a 404.
 *
 * Server Component — the auth gate runs in the parent
 * <c>(app)/layout.tsx</c> via <c>AuthGate</c>. This page is
 * server-rendered, so a returning user with a still-valid cookie
 * gets a real HTML response with the placeholder shell.
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
            Signed in. The dashboard with your alerts, the channel-mode
            matrix, and the "test this alert" button lands in wave 4.
          </Typography>
        </Stack>
        <Alert severity="info">
          <AlertTitle>Wave 3 stub</AlertTitle>
          This page is a placeholder so the sign-in / sign-up flow has
          somewhere to land. The real dashboard arrives with the
          alert-CRUD PR.
        </Alert>
      </Stack>
    </Container>
  );
}
