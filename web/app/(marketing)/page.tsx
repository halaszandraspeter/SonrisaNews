import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * Public marketing landing page. Server Component — no client state.
 * Wave 1: hero + "how it works" stub. Real copy lands in wave 11 (polish).
 */
export default function MarketingHomePage() {
  return (
    <Container maxWidth="md" component="main">
      <Stack spacing={6} sx={{ py: { xs: 6, md: 10 } }}>
        <Stack spacing={2}>
          <Typography variant="h2" component="h1">
            Sonrisa News
          </Typography>
          <Typography variant="h5" component="p" color="text.secondary">
            Free, open-source alerts for the world — breaking news, market movements, natural
            disasters.
          </Typography>
        </Stack>

        <Box>
          <Button variant="contained" size="large" aria-disabled disabled>
            Sign up (coming soon)
          </Button>
        </Box>

        <Typography variant="body2" color="text.secondary">
          Status: wave-1 skeleton. The API is reachable at port 5080, the Aspire dashboard is at
          port 15000.
        </Typography>
      </Stack>
    </Container>
  );
}
