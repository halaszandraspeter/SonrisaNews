import { createTheme } from "@mui/material/styles";

/**
 * MUI v9 theme. Wave 1 ships a single light theme; the dark theme is
 * added in wave 11 (polish).
 *
 * No `"use client"` — `createTheme` returns a plain object that is
 * safe to import from a Server Component, and `<ThemeProvider>`
 * (the only consumer) is already inside a client component
 * (`AppProviders.tsx`).
 */
export const theme = createTheme({
  cssVariables: true,
  palette: {
    mode: "light",
    primary: { main: "#1976d2" },
    secondary: { main: "#9c27b0" },
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily:
      '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, "Open Sans", "Helvetica Neue", sans-serif',
  },
});
