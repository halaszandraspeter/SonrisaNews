import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * 404 page for the <c>(app)</c> route group. Shown when a user
 * navigates to <c>/alerts/typo</c>, <c>/alerts/9999</c>, etc.
 */
export default function AppNotFound() {
  return (
    <Container maxWidth="md" component="main">
      <Stack spacing={2} sx={{ py: { xs: 6, md: 10 } }}>
        <Typography variant="h4" component="h1">
          Page not found
        </Typography>
        <Typography variant="body1" color="text.secondary">
          The page you tried to reach does not exist. Head back to
          the dashboard.
        </Typography>
      </Stack>
    </Container>
  );
}
