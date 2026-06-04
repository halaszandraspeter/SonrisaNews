import { redirect } from "next/navigation";
import type { ReactNode } from "react";

/**
 * Layout for the authenticated `(app)` route group.
 * Wave 1: placeholder that redirects to the marketing page — auth gate
 * is added in wave 3 (Auth + RBAC).
 */
export default function AppLayout({ children }: { children: ReactNode }) {
  redirect("/");
  return <>{children}</>;
}
