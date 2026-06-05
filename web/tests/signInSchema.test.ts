import { describe, expect, it } from "vitest";

import { signInSchema } from "@/app/(auth)/signin/signInSchema";

describe("signInSchema", () => {
  it("accepts a valid email + password", () => {
    const result = signInSchema.safeParse({ email: "ada@example.com", password: "hunter2hunter2" });
    expect(result.success).toBe(true);
  });

  it("rejects an empty email", () => {
    const result = signInSchema.safeParse({ email: "", password: "hunter2hunter2" });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.some((i) => i.path[0] === "email")).toBe(true);
    }
  });

  it("rejects a malformed email", () => {
    const result = signInSchema.safeParse({ email: "not-an-email", password: "hunter2hunter2" });
    expect(result.success).toBe(false);
  });

  it("rejects an empty password", () => {
    const result = signInSchema.safeParse({ email: "ada@example.com", password: "" });
    expect(result.success).toBe(false);
  });
});
