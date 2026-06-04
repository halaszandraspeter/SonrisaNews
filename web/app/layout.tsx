import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";

import { AppProviders } from "@/components/AppProviders";

import "@/styles/globals.css";

export const metadata: Metadata = {
  title: {
    default: "Sonrisa News",
    template: "%s · Sonrisa News",
  },
  description:
    "Free, open-source alerts for the world — breaking news, market movements, natural disasters.",
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
