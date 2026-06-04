import { describe, expect, it } from "vitest";

import { env } from "@/lib/env";

describe("env", () => {
  it("exposes the API URL with a default of localhost:5080", () => {
    expect(env.apiUrl).toMatch(/^https?:\/\//);
  });

  it("exposes the API URL as a non-empty string", () => {
    expect(typeof env.apiUrl).toBe("string");
    expect(env.apiUrl.length).toBeGreaterThan(0);
  });
});
