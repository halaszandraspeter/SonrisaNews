import type { ReactNode } from "react";

import { AuthGate } from "@/components/AuthGate";

/**
 * Layout for the authenticated <c>(app)</c> route group. The
 * <c>AuthGate</c> runs the sign-in redirect; once signed in, the
 * children render. Per the handoff, the boundary triplet
 * (<c>loading.tsx</c>, <c>error.tsx</c>, <c>not-found.tsx</c>)
 * ships with the first real page in this group.
 */
export default function AppLayout({ children }: { children: ReactNode }) {
  return <AuthGate>{children}</AuthGate>;
}
