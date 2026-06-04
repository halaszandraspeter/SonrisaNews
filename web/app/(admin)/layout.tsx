import { redirect } from "next/navigation";
import type { ReactNode } from "react";

/**
 * Layout for the admin `(admin)` route group.
 * Wave 1: placeholder. Auth + role check (Admin only) lands in wave 10.
 */
export default function AdminLayout({ children }: { children: ReactNode }) {
  redirect("/");
  return <>{children}</>;
}
