import { expect, test } from "@playwright/test";

/**
 * Smoke test: the marketing page renders. The "Sign up" button is
 * disabled in wave 1 — we're only asserting that the page is up.
 */
test("marketing home renders", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1, name: "Sonrisa News" })).toBeVisible();
});
