import type { ReactNode } from "react";

/**
 * Layout for the `(auth)` route group (sign-in, sign-up, verify).
 * No auth gate here — these pages are for unauthenticated users.
 * Real forms land in wave 3.
 */
export default function AuthLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
