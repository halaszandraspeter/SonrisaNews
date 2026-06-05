import { describe, expect, it } from "vitest";

import { useAuthStore } from "@/lib/auth/authStore";

describe("useAuthStore", () => {
  it("starts signed out and initializing", () => {
    const state = useAuthStore.getState();
    expect(state.userId).toBeNull();
    expect(state.email).toBeNull();
    expect(state.accessToken).toBeNull();
    expect(state.permissions).toEqual([]);
    expect(state.isInitializing).toBe(true);
  });

  it("setSession stores the user id, email, and access token, and clears permissions", () => {
    useAuthStore.getState().setSession({
      UserId: "11111111-1111-1111-1111-111111111111",
      Email: "ada@example.com",
      DisplayName: "Ada",
      Status: "Active",
      AccessToken: "abc",
      AccessTokenExpiresAt: "2030-01-01T00:00:00Z",
    });

    const state = useAuthStore.getState();
    expect(state.userId).toBe("11111111-1111-1111-1111-111111111111");
    expect(state.email).toBe("ada@example.com");
    expect(state.accessToken).toBe("abc");
    expect(state.permissions).toEqual([]);
  });

  it("setAccessToken updates only the token", () => {
    useAuthStore.getState().setSession({
      UserId: "id",
      Email: "a@b.c",
      DisplayName: "A",
      Status: "Active",
      AccessToken: "old",
      AccessTokenExpiresAt: "2030-01-01T00:00:00Z",
    });
    useAuthStore.getState().setAccessToken("new");

    const state = useAuthStore.getState();
    expect(state.accessToken).toBe("new");
    expect(state.email).toBe("a@b.c");
    expect(state.userId).toBe("id");
  });

  it("setPermissions stores the granted permission set", () => {
    useAuthStore.getState().setSession({
      UserId: "id",
      Email: "a@b.c",
      DisplayName: "A",
      Status: "Active",
      AccessToken: "tok",
      AccessTokenExpiresAt: "2030-01-01T00:00:00Z",
    });
    useAuthStore.getState().setPermissions(["Alerts.Read.Own", "Channels.Write.Own"]);

    expect(useAuthStore.getState().permissions).toEqual([
      "Alerts.Read.Own",
      "Channels.Write.Own",
    ]);
  });

  it("clearSession forgets the user id, email, token, and permissions", () => {
    useAuthStore.getState().setSession({
      UserId: "id",
      Email: "a@b.c",
      DisplayName: "A",
      Status: "Active",
      AccessToken: "tok",
      AccessTokenExpiresAt: "2030-01-01T00:00:00Z",
    });
    useAuthStore.getState().setPermissions(["X"]);
    useAuthStore.getState().clearSession();

    const state = useAuthStore.getState();
    expect(state.userId).toBeNull();
    expect(state.email).toBeNull();
    expect(state.accessToken).toBeNull();
    expect(state.permissions).toEqual([]);
  });

  it("finishInitializing is idempotent (no-ops once already finished)", () => {
    useAuthStore.getState().finishInitializing();
    expect(useAuthStore.getState().isInitializing).toBe(false);
    useAuthStore.getState().finishInitializing();
    expect(useAuthStore.getState().isInitializing).toBe(false);
  });

  it("subscribe fires the listener on every state change, and stops firing after unsubscribe", () => {
    let calls = 0;
    const unsubscribe = useAuthStore.subscribe(() => {
      calls += 1;
    });
    useAuthStore.getState().setAccessToken("tok");
    useAuthStore.getState().clearSession();
    unsubscribe();
    useAuthStore.getState().setAccessToken("after-unsub");

    expect(calls).toBe(2);
  });
});
