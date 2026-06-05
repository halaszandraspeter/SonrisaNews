"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v16-appRouter";
import { useState, type ReactNode } from "react";

import { AuthProvider } from "@/lib/auth/AuthProvider";
import { theme } from "@/styles/theme";

/**
 * Top-level client provider stack.
 *
 * Why a single component: the four providers (theme, query, emotion
 * cache, auth) all want to be the outermost wrapper. Splitting them
 * per route group would mean 4 different layouts. One root keeps it
 * simple.
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
          mutations: {
            retry: 1, // mutations shouldn't retry as eagerly as queries; one retry covers a transient 5xx.
          },
        },
      }),
  );

  return (
    <AppRouterCacheProvider>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>
          <AuthProvider>{children}</AuthProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </AppRouterCacheProvider>
  );
}
