import { describe, expect, it } from "vitest";

import { getApiUrl } from "@/lib/env";

describe("getApiUrl", () => {
  it("returns the dev default when the env var is undefined", () => {
    expect(getApiUrl(undefined)).toBe("http://localhost:5080");
  });

  it("returns the dev default when the env var is the empty string", () => {
    // An empty `NEXT_PUBLIC_API_URL` is operationally a misconfig, but
    // the function must still fall back to a usable URL rather than
    // passing "" down to openapi-fetch.
    expect(getApiUrl("")).toBe("http://localhost:5080");
  });

  it("returns the supplied value when set and non-empty", () => {
    expect(getApiUrl("https://api.example.com")).toBe("https://api.example.com");
  });
});
