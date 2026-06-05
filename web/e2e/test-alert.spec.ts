import { expect, test } from "@playwright/test";

/**
 * E2E for the "Test this alert" button (wave 6).
 *
 * Steps (mvp-checklist wave 6 §2 verify):
 *   - Sign in as the seeded admin via the UI.
 *   - Open the alerts page.
 *   - Click "Test" on a row.
 *   - Assert the dialog shows a matcher run (either an empty-
 *     state message or a list of would-have-fired events).
 *
 * The dev stack (AppHost + backend + MailHog) must be running
 * for the API calls to land. The Playwright config starts the
 * Next.js dev server automatically; the user is responsible for
 * `./scripts/dev.sh` on the host. The test is tagged so
 * `pnpm test:e2e --grep "test alert"` runs only this file.
 *
 * Credentials: the admin email + password come from
 * `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` env vars (set these
 * in CI or in a `.env.test` for local runs). If the env vars
 * are missing AND the dev stack is up, the test falls back to
 * the dev-seed defaults (`admin@sonrisa.local` /
 * `REPLACE_ME_admin_password_change_on_first_login` from
 * `appsettings.Development.json`). If neither is set, the test
 * skips with a clear message — never silently passes on a
 * dead stack.
 *
 * The sign-in step races a URL-change wait against a 5s timer.
 * If the URL never moves (bad credentials, server rejected the
 * sign-in, the form hung), the test fails fast with a clear
 * "couldn't sign in" message rather than waiting the full
 * 10s URL-wait timeout. Catches the "CI has a typo in the
 * secret" failure mode in seconds, not minutes.
 */

const DEFAULT_DEV_EMAIL = "admin@sonrisa.local";
const DEFAULT_DEV_PASSWORD = "REPLACE_ME_admin_password_change_on_first_login";
const SIGN_IN_TIMEOUT_MS = 5_000;

test("clicking Test on an alert shows the would-have-fired list", async ({ page, request }) => {
  // Probe the API. If the dev stack isn't up, skip with a
  // clear message — no point waiting for the full sign-in
  // round-trip on a dead backend.
  const health = await request.get("/api/v1/health");
  if (!health.ok()) {
    test.skip(true, `Dev stack not reachable: GET /api/v1/health → ${health.status()}. Run ./scripts/dev.sh first.`);
  }

  const email = process.env.E2E_ADMIN_EMAIL ?? DEFAULT_DEV_EMAIL;
  const password = process.env.E2E_ADMIN_PASSWORD ?? DEFAULT_DEV_PASSWORD;

  // Sign in via the UI. The access token is held in memory
  // (Zustand store); the form's submit is the only path that
  // populates it from a real sign-in round-trip.
  await page.goto("/signin");
  await page.getByLabel(/^email$/i).fill(email);
  await page.getByLabel(/^password$/i).fill(password);
  await page.getByRole("button", { name: /^sign in$/i }).click();

  // Wait for the post-sign-in redirect to /alerts. The regex
  // tolerates an optional trailing slash and a query string
  // (a future "fresh=1" hint or similar won't break the
  // redirect-detection). Wrap the wait in a try/catch so the
  // explicit 5s timer (below) is the actual race winner; the
  // built-in `waitForURL` timeout throws on expiry and would
  // short-circuit the Promise.race before the "timeout" branch
  // can run. The catch converts the throw into the same
  // `"timeout"` sentinel so the failure path is uniform.
  const signInResult = await Promise.race([
    page
      .waitForURL(/\/alerts\/?(\?.*)?$/, { timeout: SIGN_IN_TIMEOUT_MS })
      .then(() => "ok" as const)
      .catch(() => "timeout" as const),
    new Promise<"timeout">((resolve) => setTimeout(() => resolve("timeout"), SIGN_IN_TIMEOUT_MS)),
  ]);
  if (signInResult === "timeout") {
    // The form might have rendered a server-side error
    // (e.g. "Wrong email or password"). Surface that to the
    // test output if present. Use `getByRole("alert")` —
    // the form wraps server errors in a `<Typography
    // role="alert">` (per SignInForm), so the role is the
    // stable contract; the text content is the diagnostic.
    const formError = await page
      .getByRole("alert")
      .textContent()
      .catch(() => null);
    // Log the env-var name (not the value) to avoid leaking
    // PII into CI logs. The user can `echo $E2E_ADMIN_EMAIL`
    // locally to see the value.
    const emailSource = process.env.E2E_ADMIN_EMAIL ? "E2E_ADMIN_EMAIL" : "DEFAULT_DEV_EMAIL";
    throw new Error(
      `Sign-in didn't redirect to /alerts within ${SIGN_IN_TIMEOUT_MS}ms ` +
        `(email source: ${emailSource}). ` +
        (formError ? `Form error: "${formError}". ` : "") +
        `Check E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD are set to a valid admin user.`,
    );
  }

  // The "Test" button is on every alert row. If the user has
  // no alerts, the empty state is shown and the test can't
  // exercise the button. Skip in that case.
  const testButtons = page.getByRole("button", { name: /^test /i });
  const count = await testButtons.count();
  if (count === 0) {
    test.skip(true, "No alerts in the seed — create one in the UI to exercise this path.");
  }

  await testButtons.first().click();

  // The dialog title includes the alert name; a generic
  // locator matches it without coupling to the seed.
  await expect(page.getByRole("heading", { name: /^test "/i })).toBeVisible();

  // The matcher runs against the most recent 50 events. With
  // no events in the seed, the dialog shows the empty-state
  // message; with events, it shows a list. Both are valid
  // outcomes for a smoke test — the contract is "the dialog
  // reaches a settled state, no spinner, no error".
  const emptyState = page.getByText(/no events in the last 50/i);
  const resultsList = page.getByRole("list", { name: /would-have-fired events/i });
  await expect(emptyState.or(resultsList)).toBeVisible();
});
