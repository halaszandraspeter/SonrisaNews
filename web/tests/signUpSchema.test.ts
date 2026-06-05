import { describe, expect, it } from "vitest";

import { signUpSchema } from "@/app/(auth)/signup/signUpSchema";

describe("signUpSchema", () => {
  it("accepts a valid sign-up payload", () => {
    const result = signUpSchema.safeParse({
      email: "ada@example.com",
      password: "hunter2hunter2",
      displayName: "Ada",
      timeZone: "UTC",
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.timeZone).toBe("UTC");
    }
  });

  it("rejects a password shorter than 8 characters", () => {
    const result = signUpSchema.safeParse({
      email: "ada@example.com",
      password: "short",
      displayName: "Ada",
      timeZone: "UTC",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.some((i) => i.path[0] === "password")).toBe(true);
    }
  });

  it("rejects an empty display name", () => {
    const result = signUpSchema.safeParse({
      email: "ada@example.com",
      password: "hunter2hunter2",
      displayName: "",
      timeZone: "UTC",
    });
    expect(result.success).toBe(false);
  });

  it("rejects a display name longer than 120 characters", () => {
    const result = signUpSchema.safeParse({
      email: "ada@example.com",
      password: "hunter2hunter2",
      displayName: "x".repeat(121),
      timeZone: "UTC",
    });
    expect(result.success).toBe(false);
  });
});
