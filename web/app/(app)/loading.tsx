import Skeleton from "@mui/material/Skeleton";
import Stack from "@mui/material/Stack";

/**
 * Loading state for the <c>(app)</c> route group. Shown while
 * Next.js streams a Server Component in the dashboard. The
 * shape mirrors the <c>AlertsPage</c> stub: heading + body + info
 * alert.
 */
export default function AppLoading() {
  return (
    <Stack spacing={4} sx={{ p: { xs: 4, md: 10 } }} aria-busy="true">
      <Skeleton variant="text" width="30%" height={40} />
      <Skeleton variant="text" width="60%" />
      <Skeleton variant="rectangular" height={120} />
    </Stack>
  );
}
