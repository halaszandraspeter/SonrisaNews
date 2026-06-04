import type { NextConfig } from "next";

import { env } from "@/lib/env";

const config: NextConfig = {
  reactStrictMode: true,
  // typedRoutes moved out of `experimental` in Next.js 16.
  typedRoutes: true,
  // Proxy /api/* to the backend so the client always hits the same origin.
  // Removes CORS from the dev experience and matches prod (where the
  // frontend and API share the host).
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${env.apiUrl}/api/:path*`,
      },
    ];
  },
};

export default config;
