import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Container from "@mui/material/Container";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * Public marketing landing page. Server Component — no client state.
 * The primary CTA points at /signup; the secondary link points at
 * /signin for returning users. Real copy lands in wave 11 (polish).
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

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
          <Button variant="contained" size="large" href="/signup">
            Sign up
          </Button>
          <Link href="/signin" variant="body2">
            Already have an account? Sign in
          </Link>
        </Stack>
      </Stack>
    </Container>
  );
}
