import type { ReactNode } from "react";

import { AuthGate } from "@/components/AuthGate";

/**
 * Layout for the admin <c>(admin)</c> route group. Wave 3 only
 * checks "signed in"; the Admin-only check (Users.Read.Any or a
 * future Admin permission) lands in wave 10 when the admin pages
 * actually exist. Per the handoff, the boundary triplet
 * (<c>loading.tsx</c>, <c>error.tsx</c>, <c>not-found.tsx</c>)
 * ships with the first real page in this group.
 */
export default function AdminLayout({ children }: { children: ReactNode }) {
  return <AuthGate>{children}</AuthGate>;
}
