import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

import { SignInForm } from "./SignInForm";

/**
 * Sign-in page. Server Component — only the form is client-side.
 * The page is a `<main>` landmark for screen readers and a
 * layout-aware container for MUI's grid.
 */
export default function SignInPage() {
  return (
    <Container maxWidth="sm" component="main">
      <Stack spacing={4} sx={{ py: { xs: 6, md: 10 } }}>
        <Stack spacing={1}>
          <Typography variant="h4" component="h1">
            Sign in
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Welcome back. Use the email and password you signed up with.
          </Typography>
        </Stack>
        <SignInForm />
      </Stack>
    </Container>
  );
}
