import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import { SignUpForm } from "./SignUpForm";

/**
 * Sign-up page. Server Component — only the form is client-side.
 */
export default function SignUpPage() {
  return (
    <Container maxWidth="sm" component="main">
      <Stack spacing={4} sx={{ py: { xs: 6, md: 10 } }}>
        <Stack spacing={1}>
          <Typography variant="h4" component="h1">
            Create your account
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Free, open-source, no card. We'll send you a verification email
            before you can receive alerts.
          </Typography>
        </Stack>
        <SignUpForm />
      </Stack>
    </Container>
  );
}
