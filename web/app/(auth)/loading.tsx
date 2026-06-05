import Skeleton from "@mui/material/Skeleton";
import Stack from "@mui/material/Stack";

/**
 * Loading state for the <c>(auth)</c> route group. Shown while
 * Next.js streams a Server Component in the auth pages.
 */
export default function AuthLoading() {
  return (
    <Stack spacing={2} sx={{ p: 4 }} aria-busy="true">
      <Skeleton variant="text" width="30%" height={40} />
      <Skeleton variant="rectangular" height={56} />
      <Skeleton variant="rectangular" height={56} />
      <Skeleton variant="rectangular" height={40} width="40%" />
    </Stack>
  );
}
