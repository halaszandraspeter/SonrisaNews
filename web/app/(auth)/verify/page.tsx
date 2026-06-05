import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * Email-verification landing. The user pastes the 6-digit code from
 * the verification email here; the form lives in the wave-4
 * <c>VerifyForm</c> (post-MVP, alongside the email-sending seam).
 * For wave 3 the page is a stub that shows the user where to paste
 * the code.
 */
export default function VerifyPage() {
  return (
    <Container maxWidth="sm" component="main">
      <Stack spacing={4} sx={{ py: { xs: 6, md: 10 } }}>
        <Stack spacing={1}>
          <Typography variant="h4" component="h1">
            Verify your email
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Paste the 6-digit code from the email we just sent. The
            verification form is a wave-4 follow-up; for now the
            backend&apos;s <code>POST /api/v1/auth/verify</code> endpoint
            is open to a curl test.
          </Typography>
        </Stack>
      </Stack>
    </Container>
  );
}
