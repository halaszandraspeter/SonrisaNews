/**
 * Auth store. A tiny, framework-agnostic state container (no Zustand
 * or React) so the OpenAPI client middleware can read the access
 * token on every request without depending on a React hook. The
 * <c>AuthProvider</c> is a thin React wrapper that subscribes to this
 * store via <c>useSyncExternalStore</c>.
 *
 * Trade-off: a hard refresh logs the user out. The handoff defers a
 * short-lived server-readable cookie to wave 11+ — for wave 3 the
 * in-memory token is the smallest correct surface. On app mount the
 * <c>AuthProvider</c> calls <c>POST /api/v1/auth/refresh</c> (the
 * cookie is sent automatically); if the refresh fails, the user
 * lands on the sign-in page.
 *
 * Design: the mutators live on the snapshot object returned by
 * <c>getState()</c>, so callers can read state and dispatch
 * actions off the same reference (no need to know about the
 * <c>useAuthStore</c> object). The mutators are stable across
 * snapshots — the function references don't change — so
 * <c>useSyncExternalStore</c>'s reference-equality check still
 * detects state changes by the snapshot *data* changing.
 */

import type { SignInResponse } from "@/lib/api/schema";

export type AuthStoreState = {
  userId: string | null;
  email: string | null;
  /** The access token, or `null` if signed out. In-memory only. */
  accessToken: string | null;
  /** Granted permission names. Empty until `loadPermissions` has been called. */
  permissions: ReadonlyArray<string>;
  /** True until the provider has finished its initial mount-time refresh probe. */
  isInitializing: boolean;
  /** Mutators. Stable references; safe to call from the snapshot. */
  setSession: (response: SignInResponse) => void;
  setAccessToken: (token: string) => void;
  setPermissions: (permissions: ReadonlyArray<string>) => void;
  clearSession: () => void;
  finishInitializing: () => void;
};

type Listener = () => void;

let state: AuthStoreState = createInitialState();
const listeners = new Set<Listener>();

function createInitialState(): AuthStoreState {
  return {
    userId: null,
    email: null,
    accessToken: null,
    permissions: [],
    isInitializing: true,
    setSession: (response) => {
      state = {
        ...state,
        userId: response.UserId,
        email: response.Email,
        accessToken: response.AccessToken,
        permissions: [],
        setSession: state.setSession,
        setAccessToken: state.setAccessToken,
        setPermissions: state.setPermissions,
        clearSession: state.clearSession,
        finishInitializing: state.finishInitializing,
      };
      emit();
    },
    setAccessToken: (token) => {
      state = { ...state, accessToken: token };
      emit();
    },
    setPermissions: (permissions) => {
      state = { ...state, permissions };
      emit();
    },
    clearSession: () => {
      state = {
        ...state,
        userId: null,
        email: null,
        accessToken: null,
        permissions: [],
      };
      emit();
    },
    finishInitializing: () => {
      if (!state.isInitializing) return;
      state = { ...state, isInitializing: false };
      emit();
    },
  };
}

function emit(): void {
  for (const l of listeners) l();
}

export const useAuthStore = {
  /** Subscribe to state changes. The returned function unsubscribes. */
  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  /** Snapshot the current state. The mutators on the snapshot are stable; the data fields change on every mutation, so reference equality is a reliable "did anything change" check. */
  getState(): AuthStoreState {
    return state;
  },
};
