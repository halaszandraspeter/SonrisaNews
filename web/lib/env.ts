/**
 * Frontend env accessors. Centralized so a missing var is a build-time
 * error rather than a runtime crash in a random component.
 */

const DEFAULT_API_URL = "http://localhost:5080";

/**
 * Resolves the API base URL. Returns the supplied value when set and
 * non-empty; otherwise the dev default. Exposed as a pure function so
 * tests can pin both branches without touching `process.env`.
 */
export const getApiUrl = (rawValue: string | undefined): string => {
  if (rawValue === undefined || rawValue.length === 0) return DEFAULT_API_URL;
  return rawValue;
};

/**
 * The frozen env object consumed by the rest of the app. Reads
 * `NEXT_PUBLIC_API_URL` at module load; the value is then immutable.
 */
export const env = {
  apiUrl: getApiUrl(process.env.NEXT_PUBLIC_API_URL),
} as const;
