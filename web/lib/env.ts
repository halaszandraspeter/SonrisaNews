/**
 * Frontend env accessors. Centralized so a missing var is a build-time
 * error rather than a runtime crash in a random component.
 */
export const env = {
  apiUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080",
} as const;
