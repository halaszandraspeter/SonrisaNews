"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";

import Skeleton from "@mui/material/Skeleton";
import Stack from "@mui/material/Stack";

import { useAuth } from "@/lib/auth/AuthProvider";

/**
 * Client-side auth gate. The handoff's rule (2026-06-05):
 * validation is by permission, not by role. This component enforces
 * the "must be signed in" check; the per-action permission check is
 * the API's job (the RbacPolicyHandler returns 403 for any
 * unauthorized action; this client just shows a Forbidden UI in
 * that case).
 *
 * Three states:
 *   - <c>isInitializing</c>: render a skeleton; don't redirect yet.
 *   - Signed out: redirect to /signin. The next.js router push
 *     happens once; subsequent renders don't push again.
 *   - Signed in: render children.
 */
export function AuthGate({ children }: { children: ReactNode }): React.JSX.Element {
  const router = useRouter();
  const { state, isAuthenticated } = useAuth();

  useEffect(() => {
    if (state.isInitializing) return;
    if (!isAuthenticated) {
      router.replace("/signin");
    }
  }, [state.isInitializing, isAuthenticated, router]);

  if (state.isInitializing || !isAuthenticated) {
    return (
      <Stack spacing={2} sx={{ p: 4 }} aria-busy="true">
        <Skeleton variant="text" width="40%" height={32} />
        <Skeleton variant="rectangular" height={120} />
        <Skeleton variant="rectangular" height={120} />
      </Stack>
    );
  }

  return <>{children}</>;
}
