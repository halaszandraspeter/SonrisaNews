import type { ReactNode } from "react";

/**
 * Layout for the marketing route group `(marketing)`.
 * Public — no auth gate. Each page here is a Server Component by default.
 */
export default function MarketingLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
