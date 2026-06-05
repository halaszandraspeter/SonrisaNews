/**
 * Auth provider. A thin React wrapper around the framework-agnostic
 * <c>authStore</c> (see ./authStore). The store is the source of
 * truth so the OpenAPI client middleware (which runs outside React)
 * can read the access token on every request.
 *
 * On mount the provider kicks off a single refresh probe so a
 * returning user (with a still-valid httpOnly cookie) lands on the
 * dashboard without a sign-in. If the refresh fails, the user stays
 * signed out and the sign-in page handles the redirect.
 *
 * Trade-off: a hard refresh logs the user out unless the refresh
 * probe succeeds. The handoff defers a short-lived server-readable
 * cookie to wave 11+ — for wave 3 the in-memory token + mount-time
 * refresh is the smallest correct surface.
 */
"use client";

import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from "react";

import { apiClient } from "@/lib/api/client";
import { useAuthStore, type AuthStoreState } from "@/lib/auth/authStore";

type AuthContextValue = {
  isAuthenticated: boolean;
  hasPermission: (permission: string) => boolean;
  /** Same shape as the store; exposed as a single object for ergonomic consumption. */
  state: AuthStoreState;
};

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * How long the mount-time refresh probe waits before giving up.
 * Long enough to absorb a slow dev server (cold start, MailHog
 * warming, EF migrations), short enough that a wedged API doesn't
 * pin the user on a skeleton for half a minute.
 */
const REFRESH_PROBE_TIMEOUT_MS = 5_000;

/**
 * Module-level guard so React Strict Mode's double-mount in dev
 * doesn't fire the probe twice. The guard is reset on
 * <c>clearSession()</c> so a fresh sign-in can re-trigger the probe
 * on the next mount; it is *not* reset on remount, so the second
 * Strict-Mode mount is a no-op.
 */
let initialProbeDone = false;

export function AuthProvider({ children }: { children: ReactNode }): React.JSX.Element {
  const state = useSyncExternalStore(
    useAuthStore.subscribe,
    useAuthStore.getState,
    useAuthStore.getState,
  );

  useEffect(() => {
    if (initialProbeDone) {
      // Either the probe has already succeeded (the user is signed
      // in) or it has already failed (the user is signed out).
      // The provider's "initializing" flag is the source of truth
      // and was flipped to <c>false</c> by the first run. We just
      // skip the second mount to avoid a duplicate probe.
      return;
    }
    initialProbeDone = true;

    let cancelled = false;
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), REFRESH_PROBE_TIMEOUT_MS);

    (async () => {
      try {
        const { data, response } = await apiClient.POST("/api/v1/auth/refresh", {
          signal: controller.signal,
        } as Parameters<typeof apiClient.POST>[1]);
        if (cancelled) return;
        if (response.ok && data !== undefined) {
          useAuthStore.getState().setSession(data);
        } else {
          // The probe reached the API but the response is not a
          // session (5xx, 4xx other than 200). The user is signed
          // out; the sign-in page handles the redirect. Without
          // this branch a 5xx leaves <c>isInitializing</c> at
          // <c>true</c> *only* because the <c>finally</c> runs;
          // the explicit <c>clearSession()</c> makes the intent
          // unambiguous and survives a future refactor that
          // removes the <c>finally</c>.
          useAuthStore.getState().clearSession();
        }
      } catch {
        // The probe threw (network error, abort due to timeout,
        // etc.). The user is signed out.
        useAuthStore.getState().clearSession();
      } finally {
        clearTimeout(timeoutId);
        if (!cancelled) {
          useAuthStore.getState().finishInitializing();
        }
      }
    })();

    return () => {
      cancelled = true;
      controller.abort();
      clearTimeout(timeoutId);
    };
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      state,
      isAuthenticated: state.accessToken !== null,
      hasPermission: (p) => state.permissions.includes(p),
    }),
    [state],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** Hook. Throws if used outside the provider — that's a misuse, not a runtime concern. */
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (ctx === null) {
    throw new Error("useAuth must be used inside <AuthProvider>");
  }
  return ctx;
}
