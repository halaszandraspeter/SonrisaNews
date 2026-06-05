import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * 404 page for the <c>(auth)</c> route group. Shown when a user
 * navigates to <c>/signin/typo</c>, <c>/signup/typo</c>, etc.
 */
export default function AuthNotFound() {
  return (
    <Container maxWidth="sm" component="main">
      <Stack spacing={2} sx={{ py: { xs: 6, md: 10 } }}>
        <Typography variant="h4" component="h1">
          Page not found
        </Typography>
        <Typography variant="body1" color="text.secondary">
          The page you tried to reach does not exist. Use the link
          in the email we sent you, or head back to the sign-in page.
        </Typography>
      </Stack>
    </Container>
  );
}
