"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v16-appRouter";
import { useState, type ReactNode } from "react";

import { theme } from "@/styles/theme";

/**
 * Top-level client provider stack.
 *
 * Why a single component: the three providers (theme, query, emotion cache)
 * all want to be the outermost wrapper. Splitting them per route group
 * would mean 3 different layouts. One root keeps it simple.
 */
export function AppProviders({ children }: { children: ReactNode }) {
  // `useState` to avoid creating a new QueryClient on every render.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: 2,
            retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10_000),
          },
        },
      }),
  );

  return (
    <AppRouterCacheProvider>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </ThemeProvider>
    </AppRouterCacheProvider>
  );
}
