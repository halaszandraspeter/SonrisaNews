# Wave 6 Frontend — Review-Fix Handoff

> **Status**: ✅ All SHOULD fixes from all four review passes applied (2026-06-05). 85/85 tests passing, typecheck + build clean. **Remaining NIT items deferred** — see §1 below.
> **Scope of this handoff**: documents the post-review changes across four revision rounds, and the NIT items reviewed and intentionally deferred.

---

## 1. Deferred follow-ups (NOT mine to fix in this wave)

### 1.1 `Results` `aria-live="polite"` for SR announcements on result update

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) — the `<Stack>` wrapping the result list (around line 145).

**The opportunity**: when the matcher resolves and the result list renders, the focus is still on the "Run again" button. A screen-reader user gets no announcement that the result content changed. Adding `aria-live="polite"` to the result region would make the new content announceable.

**Why deferred**: MUI's dialog focus trap covers most SR scenarios, and the dialog's `aria-label` + the `<DialogTitle>` give the SR a stable region. A future wave-11 polish PR can add `aria-live` once the SR testing pattern is established for the project.

### 1.2 `autoFocus` only on the first open

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) line 130.

**The opportunity**: the Close button has `autoFocus` which is correct on the first open. On subsequent re-opens (the user closes the dialog, then opens it again), the focus jumps back to Close even if the user was reading the result list. A `useRef` + `useEffect` to set `autoFocus` only on the first render per `alertId` would preserve the user's reading position on re-opens.

**Why deferred**: a real UX improvement but the current behavior is acceptable. A 5-line change with a state machine in scope for a wave-11 polish PR.

### 1.3 Complementary "plural grammar" test

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx).

**The opportunity**: the existing "uses singular grammar" test asserts the plural form is *not* present in the singular case. A complementary test asserting the plural form *is* present in the multi-hit case would make the singular/plural contract more readable. The current coverage is sufficient (the singular test does the right assertion).

**Why deferred**: cosmetic. The contract is correct; the test name + assertion are clear.

### 1.4 `vi.mock` ESM-migration note

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx) lines 10–17.

**The opportunity**: the `vi.mock("@/lib/api/client", …)` declaration sits *above* the import. Vitest's hoisting makes this work, but a future migration to `vi.mock` ESM (vitest v5+) may break. A JSDoc on the `vi.mock` block would document the constraint.

**Why deferred**: the project is on vitest 4.x; the migration is a global tooling decision, not a wave-6 one. When vitest 5 lands, the test file is a 5-line fix.

### 1.5 Lint config (pre-existing)

**File**: [`web/.eslintrc.cjs`](../../web/.eslintrc.cjs) (legacy) vs. ESLint v9's required `eslint.config.js`.

**The opportunity**: `pnpm lint` exits 1 with "ESLint couldn't find an eslint.config.(js|mjs|cjs) file." A migration to the flat config format would unblock the lint script.

**Why deferred**: pre-existing — not my concern. The project doesn't run lint in CI. A wave-11 polish item.

### 1.6 `runAgain` empty `catch {}` ESLint disable comment

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) line 80-86.

**The opportunity**: the empty `catch {}` block in `runAgain` has a JSDoc explaining the intent, but a future ESLint flat config (per §1.5) with `@typescript-eslint/no-empty-function` enabled will flag it. Adding `// eslint-disable-next-line @typescript-eslint/no-empty-function` would suppress the warning. The marker is the conventional way to say "deliberately empty."

**Why deferred**: the lint config is broken pre-existing (per §1.5); the suppression isn't needed today. When the lint config migrates, this is a 1-line addition.

### 1.7 `runAgain` `mutation.reset()` before `mutateAsync` brief idle flash

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) lines 78-86.

**The opportunity**: `runAgain` calls `mutation.reset()` *before* `mutateAsync()`. `reset()` clears `data` / `error` / `isError`, flipping the state to idle momentarily. The sequence is: reset → state idle → mutateAsync → state pending → state settled. The "idle" frame in between means a screen reader may briefly announce "no events" before the new result lands.

**Why deferred**: a UX nit, not a correctness bug. The `useEffect` on `open` does the same (reset + mutate) and the user-visible flash on re-open is a non-issue because the dialog is just opening. Recommend: drop the `mutation.reset()` call in `runAgain` — let the prior result stay visible during the in-flight period, and let `mutateAsync` replace the data on resolve. A wave-11 polish item.

### 1.8 Multi-hit / empty-state test secondary text assertions

**Files**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx).

**The opportunity**: the "renders a list of summaries when the matcher returns hits" test asserts the count text and the summary strings, but not the `Event {EventId.slice(0, 8)}` secondary text. The "shows an empty-state message" test asserts the primary "no events in the last 50" but not the secondary "Polling has to have fetched at least one event …" caption. Future refactors that break the secondary copy would slip through.

**Why deferred**: cosmetic. The primary contract (count, summaries) is the user-visible behavior; the secondary text is informational. A wave-11 polish item.

### 1.9 mvp-checklist cross-reference stability

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 7.

**The opportunity**: the file-level JSDoc references "mvp-checklist wave 6 §2 verify" — a stable reference today, but the mvp-checklist is a moving target. A future refactor renumbering the waves would make the reference stale. A search-friendly string ("the wave 6 'Test alert' e2e") would survive a renumbering.

**Why deferred**: cosmetic. The cross-reference is useful today; the future-proofing is a wave-11 polish item.

### 1.10 `react/jsx-curly-brace-presence` ESLint concern on `{" "}`

**File**: [`web/app/(app)/alerts/page.tsx`](../../web/app/(app)/alerts/page.tsx) line 31.

**The opportunity**: the line `or hit{" "}` followed by `<strong>Test</strong>` uses a JSX expression to render a single space. This is the correct pattern, but ESLint's `react/jsx-curly-brace-presence` rule (when enabled) may flag it. The current pre-existing lint config doesn't run, so this is not a CI failure — but a future migration to the flat config (per §1.5) will need this rule either disabled or the pattern refactored.

**Why deferred**: tied to the §1.5 lint config migration. When that lands, the `{" "}` is a 1-line refactor (or a 1-line lint-disable).

### 1.11 Wave 6 banner copy vs. fresh-user context

**File**: [`web/app/(app)/alerts/page.tsx`](../../web/app/(app)/alerts/page.tsx) line 36-39.

**The opportunity**: the `<AlertTitle>` says "Wave 6 — news poller + matcher". A fresh user landing on `/alerts` (no prior context) sees "Wave 6" without context — it reads as a half-finished product. Consider "What's new" as the prefix, or move the wave tag to a hidden `data-wave` attribute and render a generic "Welcome — alerts are live" message.

**Why deferred**: a product copy call, not a code change. The user-facing copy is for the product team, not the agent.

### 1.12 E2E `^email$` regex anchor fragility

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 60.

**The opportunity**: `getByLabel(/^email$/i)` — the `^…$` anchors are fragile to copy changes. A future "Email Address" label would break the regex. A `getByLabel(/email/i)` (no anchors) would match both "Email" and "Email Address" with a smaller risk of accidental partial matches.

**Why deferred**: cosmetic. The current copy is "Email"; a future refactor would need to update the test anyway. A wave-11 polish item.

### 1.13 JSDoc Concurrency section duplication

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) lines 21-25 and 76-78.

**The opportunity**: the file-level JSDoc has a "Concurrency" section (lines 21-25) and the `runAgain` function-level JSDoc has the same content (lines 76-78). Two copies of the same content will drift.

**Why deferred**: cosmetic. The duplication is small (3 lines) and both copies are correct today. A wave-11 polish item.

### 1.14 E2E top-of-file JSDoc duplication

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) lines 23-26 and the inline comment on lines 65-71.

**The opportunity**: the top-of-file JSDoc says "If the URL never moves (bad credentials, server rejected the sign-in, the form hung), the test fails fast with a clear 'couldn't sign in' message rather than waiting the full 10s URL-wait timeout." The inline comment on lines 65-71 says the same thing plus the implementation detail (the `.then(…).catch(() => "timeout")` is needed because `waitForURL`'s built-in throw short-circuits the race). The two explanations overlap; the implementation note in the inline comment is the load-bearing part; the top-of-file JSDoc can trim to a one-line summary.

**Why deferred**: cosmetic. The duplication is informative (a future reader benefits from seeing both the "why" and the "how"). A wave-11 polish item.

### 1.15 E2E error message — `emailSource` line + "check" line could be one

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 88.

**The opportunity**: the failure message has `email source: ${emailSource}` on one line and `Check E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD are set to a valid admin user.` on the next. The two lines refer to the same env vars; a user skimming the error might miss the connection. Recommend: `Check E2E_ADMIN_EMAIL (currently: ${emailSource}) / E2E_ADMIN_PASSWORD are set to a valid admin user.` — the env-var name and the current source are in the same sentence.

**Why deferred**: cosmetic. The current message is correct and the `email source` line is grep-able. A wave-11 polish item.

### 1.16 E2E `waitForURL` throw-vs-resolve explanation

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 65.

**The opportunity**: the inline comment glosses over the subtle "throw short-circuits the race" detail. A future reader who changes the timeout to 10s or 2s might not understand *why* the explicit `setTimeout` is needed. The comment should explicitly say: "the built-in `waitForURL` timeout *throws* on expiry (rather than resolving), which would skip the `Promise.race` and surface the generic 'timed out' message instead of our diagnostic. The `.catch(() => "timeout")` converts the throw to a sentinel so the race resolves cleanly."

**Why deferred**: cosmetic. The current code is correct; the comment is a future-reader aid. A wave-11 polish item.

### 1.17 50ms `setTimeout` smell in race-fix test

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx) (revised in §4.5 to use `setTimeout(resolve, 0)`).

**The opportunity**: a 50ms wait is a code smell. The current 0-tick yield is sufficient (the handler is fully sync on the success path) but a `vi.waitFor(() => expect(apiClient.POST).toHaveBeenCalledTimes(1), { timeout: 200 })` would be more explicit.

**Why deferred**: cosmetic. The 0-tick yield works because the handler is sync; documenting that the test is sync-tolerance-dependent is in the comment block. A wave-11 polish item.

### 1.18 `fireEvent.click` "bypass" comment clarity

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx).

**The opportunity**: the comment on the `fireEvent.click` line says "bypass the disabled check" — but `fireEvent.click` doesn't bypass `disabled`. The button is *visually* disabled, but `fireEvent.click` dispatches a synthetic click regardless. `userEvent.click` would respect the disabled state. Recommend: clarify the comment to say "force a click that real users couldn't perform (`userEvent.click` would respect the `disabled` prop and no-op; `fireEvent.click` dispatches the event regardless)."

**Why deferred**: cosmetic. The current comment is correct in spirit (it bypasses what real users see). A wave-11 polish item.

### 1.19 NIT-1: race-fix test two comment blocks

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx).

**The opportunity**: a future revision may want to merge the test's pre-amble comment into a single block. (Already partially addressed in §4.5 — the comment was refactored but the structure is still multi-block.) A wave-11 polish item.

### 1.20 NIT-2: e2e DEFAULT_DEV_PASSWORD placeholder comment

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 33.

**The opportunity**: `const DEFAULT_DEV_PASSWORD = "REPLACE_ME_admin_password_change_on_first_login";` is a placeholder string. A user reading the test file and trying the password literally will get rejected. A comment like "(intentionally a non-functional placeholder — see `appsettings.Development.json`)" would prevent the "why doesn't this work?" debugging loop.

**Why deferred**: cosmetic. The `DEFAULT_DEV_*` naming convention signals "dev default, not real." A wave-11 polish item.

### 1.21 NIT-3: e2e `getByRole("alert")` ambiguity

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 78.

**The opportunity**: `getByRole("alert")` with no name matcher will throw if zero or multiple elements match. A future refactor that adds a second `role="alert"` element (e.g. a top-level error boundary) would cause this to throw. The `.catch(() => null)` swallows the throw, but the test is silent on a real bug. Recommend: `getByRole("alert").first().textContent()` to be explicit about "first alert" (or pass a name matcher to disambiguate).

**Why deferred**: cosmetic. The current pattern works. A wave-11 polish item.

### 1.22 NIT-4: e2e `getByLabel(/^email$/i)` fragility

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 60.

**The opportunity**: `getByLabel(/^email$/i)` — the `^…$` anchors are fragile to copy changes (per §1.12, related concern). A `getByLabel("Email")` (no regex) is what other tests in the project use and is the simplest form for a static label.

**Why deferred**: cosmetic. The current pattern is over-engineered for a static label. A wave-11 polish item.

### 1.23 NIT-5: e2e `.or()` pattern extraction

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts) line 103-105.

**The opportunity**: `const emptyState = page.getByText(...); const resultsList = page.getByRole(...); await expect(emptyState.or(resultsList)).toBeVisible();` — the `.or()` pattern is correct. If a future maintainer adds a third outcome (e.g. "loading mid-flight"), the `.or()` chain would need to be extended. Recommend: extract the "settled state" assertion to a named helper. Out of scope for wave 6.

**Why deferred**: cosmetic. A wave-11 polish item.

---

## 2. SHOULD fixes applied in revision 1 (post-first review)

### 2.1 E2E spec — env-var credentials with sensible defaults

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: hard-coded `ada@example.com` / `SonrisaNews!1`. The admin email is configurable via `SEED_ADMIN_EMAIL`; the password isn't committed. The test would silently skip on every CI run.

**After**: reads `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` from env, with a fallback to the dev-seed defaults (`admin@sonrisa.local` / `REPLACE_ME_admin_password_change_on_first_login` from `appsettings.Development.json`). If neither is set and the dev stack is up, the test runs against the dev seed. If the dev stack is down, the test skips with a clear message.

### 2.2 E2E URL regex — tolerates `?fresh=1` and trailing slash

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: `page.waitForURL(/\/alerts$/, …)` — strict end-of-string match.

**After**: `page.waitForURL(/\/alerts\/?(\?.*)?$/, …)` — tolerates `/alerts`, `/alerts/`, `/alerts?fresh=1`, `/alerts/?fresh=1`. A future "fresh=1" hint or trailing-slash normalization won't break the redirect detection.

### 2.3 `useEffect` deps — `[open, alertId]`

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx).

**Before**: `useEffect(…, [open])` — only re-fired on the open transition.

**After**: `useEffect(…, [open, alertId])` — re-fires on the open transition AND when `alertId` changes. The JSDoc on the effect explains the cadence.

### 2.4 Redundant `aria-label` on "Run again" button

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx).

**Before**: `<Button onClick={runAgain} disabled={mutation.isPending} aria-label="Run again">Run again</Button>`.

**After**: dropped the `aria-label`. The visible text serves as the accessible name.

### 2.5 Copy fix: "click the row" → "click the alert's name"

**File**: [`web/app/(app)/alerts/page.tsx`](../../web/app/(app)/alerts/page.tsx).

**Before**: "click an alert's row to open the channel-mode matrix" — but the row is not a link; only the alert name (the `<Typography component={Link}>`) is.

**After**: "click an alert's name to open the channel-mode matrix".

### 2.6 Empty-summary distinct UI treatment

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx).

**Before**: hits with empty `Summary` rendered as `"(no summary)"` (a string literal). 50 items of "(no summary)" was ambiguous.

**After**: extracted `HitSummary` sub-component. Empty summaries render as a `<Chip size="small" label="empty payload" />` plus a small caption. Test added: "surfaces empty-payload hits as a distinct 'empty payload' chip".

### 2.7 Drop unused `export` from `TestAlertDialogProps`

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx).

**Before**: `export type TestAlertDialogProps = { … }` — the test uses structural extraction (`React.ComponentProps<typeof TestAlertDialog>`), so the named export was unused.

**After**: `type TestAlertDialogProps = { … }` (no export). The test still works.

### 2.8 Drop wave numbers from JSDoc

**Files**: [`web/app/(app)/alerts/page.tsx`](../../web/app/(app)/alerts/page.tsx), [`web/features/alerts/AlertsPageBody.tsx`](../../web/features/alerts/AlertsPageBody.tsx), [`web/features/alerts/TestAlertButton.tsx`](../../web/features/alerts/TestAlertButton.tsx).

**Before**: JSDoc said "Wave 6 ships the full alert list" — the comment goes stale the moment wave 7 lands.

**After**: the JSDoc describes the surface, not the wave. The wave-tagged user-facing message is in `<AlertTitle>`, which is the right place.

### 2.9 Trim implementation detail from e2e spec docstring

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: the file-level JSDoc mentioned "the wave 6 dialog uses a useEffect to fire the mutation on `open=true`" — implementation detail.

**After**: the JSDoc describes the *behavior under test* (sign in → click Test → see would-have-fired list or empty state) and the env-var fallback for credentials.

---

## 3. SHOULD fixes applied in revision 2 (post-second review)

### 3.1 `Run again` race fix — `mutateAsync` + `await` + `isPending` guard

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) — the `runAgain` function.

**Before**: `runAgain` was synchronous — `mutation.reset(); mutation.mutate();`. React Query's `useMutation` has no built-in cancel for fire-and-forget mutations, so a click that lands between the in-flight `isPending` flipping to `false` and React batching the state update could fire a second `mutate()` before the first resolves. The `disabled={mutation.isPending}` flag prevented the user-driven race, but the keyboard + state-transition race was real.

**After**: `runAgain` is now async, with three lines of defense:

```ts
const runAgain = async () => {
  if (mutation.isPending) return; // belt + suspenders
  mutation.reset();
  try {
    await mutation.mutateAsync();
  } catch {
    // The error is already reflected via `mutation.isError`
    // in the rendered view; the `await` is here for the
    // sequencing guarantee, not for surfacing the error.
  }
};
```

The `isPending` guard is the primary defense; the `mutateAsync` + `await` is the secondary defense (it ensures the second `mutateAsync` doesn't land until the first resolves). The button's `disabled` flag is the third defense (UX layer).

**Test added**: "ignores a Run-again click while the previous matcher run is in flight" — uses a never-resolving promise and `fireEvent.click` to bypass the `disabled` check (mirroring the keyboard + state-transition race). Asserts the count stays at 1.

The existing "re-runs the matcher" test was also updated to wait for the first call to resolve before clicking "Run again" — the new async handler awaits the prior promise before firing the next.

### 3.2 E2E sign-in failure path — race URL change against a hard timer

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: `await page.waitForURL(…)` would time out after 10 seconds with no diagnostic information if the env-var credentials were wrong. A "CI has a typo in the secret" failure took 10 seconds to diagnose.

**After**: the URL change is raced against a 5-second hard timer. If the URL never moves, the test fails fast with a clear "couldn't sign in" message that includes:

- The email used (so the user can see the typo)
- Any form error text ("Wrong email or password" / "Sign-in failed") extracted from the DOM
- A hint to check `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD`

The 5-second timer is intentionally shorter than the 10-second `waitForURL` default — a healthy sign-in round-trip on a local dev stack is sub-second, so 5s is plenty of headroom. A 10s wait would only mask a real bug.

### 3.3 E2E `getByRole` → `getByLabel` for consistency

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: `page.getByRole("textbox", { name: /^email$/i })` — works in MUI v9 but is fragile if a future refactor wraps the TextField in a custom `<label>`.

**After**: `page.getByLabel(/^email$/i)` — consistent with the project's other tests (`AlertEditorDialog.test.tsx`, etc.). The password field was already using `getByLabel`.

### 3.4 Page-level copy "or hit" line break fix

**File**: [`web/app/(app)/alerts/page.tsx`](../../web/app/(app)/alerts/page.tsx).

**Before**: "click an alert's name to open the channel-mode matrix or hit\n<strong>Test</strong>" — the strong break put "Test" on its own visual line, which read as "or hit [enter] Test".

**After**: "click an alert's name to open the channel-mode matrix, or hit <strong>Test</strong> to preview what would fire right now" — single inline line, no visual break, with a `{" "}` to ensure the space between "hit" and "<strong>" is preserved by the JSX whitespace handling.

### 3.5 `HitSummary` caption user-facing phrasing

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) — the `HitSummary` sub-component.

**Before**: "the matched event had no title / summary / description" — the user doesn't know what "title / summary / description" maps to in `NewsMatcher.Summarize`. Developer-speak leaking into the UI.

**After**: "the matched event had no extractable summary" — same point, no internal terminology. The chip label "empty payload" is unchanged (it's the right level of abstraction).

### 3.6 NIT-6: drop "wave-6" qualifier from dialog JSDoc

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) — the JSDoc header.

**Before**: "Wired to the wave-6 backend <c>POST /api/v1/alerts/{id}/test</c>." — same wave-tag problem the page-level JSDoc had.

**After**: "Wired to <c>POST /api/v1/alerts/{id}/test</c>." — drop the "wave-6" qualifier.

### 3.7 NIT-2: `useEffect` `key={alertId}` recommended-pattern comment

**File**: [`web/features/alerts/TestAlertDialog.tsx`](../../web/features/alerts/TestAlertDialog.tsx) — the JSDoc on the `useEffect`.

**After**: added a note: "If a flash of stale content during an `alertId` transition is unacceptable, the parent should re-mount the dialog with `key={alertId}`." The current implementation is correct; the comment is preventive for the next refactor.

---

## 4. SHOULD fixes applied in revision 3 (post-third review)

### 4.1 E2E sign-in failure path — `try`/`catch` so the explicit 5s timer wins the race

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: `page.waitForURL(…, { timeout: 5_000 })` would throw on expiry, short-circuiting the `Promise.race` and bypassing the `"timeout"` branch. The explicit `setTimeout(…, 5_000)` was dead code in the success path; the failure path used the wait's built-in timeout, which produces a generic "timed out" message with no diagnostic context.

**After**: wrapped the wait in `.then(…).catch(() => "timeout")` so the throw is converted to the same `"timeout"` sentinel as the explicit timer. The `Promise.race` now resolves cleanly with `"timeout"` on either path. The "couldn't sign in" branch runs, the form error is extracted, and the clear "check `E2E_ADMIN_EMAIL`" hint surfaces.

```ts
const signInResult = await Promise.race([
  page
    .waitForURL(/\/alerts\/?(\?.*)?$/, { timeout: SIGN_IN_TIMEOUT_MS })
    .then(() => "ok" as const)
    .catch(() => "timeout" as const),
  new Promise<"timeout">((resolve) => setTimeout(() => resolve("timeout"), SIGN_IN_TIMEOUT_MS)),
]);
```

### 4.2 E2E sign-in failure — privacy-safe error message

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: the failure message embedded `(email: ${email})` — the actual email value. If CI sets `E2E_ADMIN_EMAIL` to a real PII value (e.g. `qa-team@yourcompany.com`), the value ends up in CI logs.

**After**: logs `email source: ${emailSource}` where `emailSource` is the env-var name (`E2E_ADMIN_EMAIL` or `DEFAULT_DEV_EMAIL`). The user can `echo $E2E_ADMIN_EMAIL` locally to see the value; CI logs stay clean.

```ts
const emailSource = process.env.E2E_ADMIN_EMAIL ? "E2E_ADMIN_EMAIL" : "DEFAULT_DEV_EMAIL";
throw new Error(
  `Sign-in didn't redirect to /alerts within ${SIGN_IN_TIMEOUT_MS}ms ` +
    `(email source: ${emailSource}). ` + …,
);
```

### 4.3 E2E sign-in failure — `getByText` → `getByRole("alert")`

**File**: [`web/e2e/test-alert.spec.ts`](../../web/e2e/test-alert.spec.ts).

**Before**: `page.getByText(/wrong email or password|sign-in failed/i)` matched the form's error text. The regex is fragile to copy changes.

**After**: `page.getByRole("alert")` — the `SignInForm` wraps server errors in a `<Typography role="alert" color="error">` (per the wave-3 form code), so the role is the stable contract; the text content is the diagnostic. Future copy changes to the form (e.g. "Email or password is incorrect") don't break the test.

### 4.4 Race-fix test — pin the UX-layer defense (button disabled) alongside the handler defense

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx) — the "ignores a Run-again click while the previous matcher run is in flight" test.

**Before**: the test asserted the call count stayed at 1 when a click bypassed the `disabled` prop. A future refactor that drops the `disabled` prop would weaken the UX without breaking the handler; the test would still pass (the handler's `isPending` guard still works). The UX-layer defense was un-pinned.

**After**: added a complementary assertion: the "Run again" button is `disabled` while the mutation is in flight. The test now pins both layers:

- **UX layer**: `expect((runAgainButton as HTMLButtonElement).disabled).toBe(true)` — the `disabled` prop is the right shape.
- **Data layer**: `expect(apiClient.POST).toHaveBeenCalledTimes(1)` — the handler's `isPending` guard catches the bypassed-click case.

A future refactor that drops the `disabled` prop would now fail the test. (Note: Vitest exposes the DOM `disabled` property directly — `toBeDisabled` is a Jest matcher, not a Vitest one.)

### 4.5 Race-fix test — fix the comment inconsistency in the test's own JSDoc

**File**: [`web/features/alerts/TestAlertDialog.test.tsx`](../../web/features/alerts/TestAlertDialog.test.tsx) — the "ignores a Run-again click while the previous matcher run is in flight" test.

**Before**: the test's JSDoc-style comment said "asserted via `toBeDisabled()`, the second via the call count" — but the actual code used `(runAgainButton as HTMLButtonElement).disabled` (Vitest has no `toBeDisabled` matcher; that's a Jest matcher). A few lines later, a *second* comment block re-explained the Vitest-vs-Jest difference. The two comment blocks were internally inconsistent — a future reader would see the "asserted via `toBeDisabled()`" line and not know whether to trust the comment or the code.

**After**: merged the two comment blocks into a single JSDoc-style block above the test body. The merged block:

- Names both layers of the race fix (UX: `disabled` prop; Data: `isPending` guard) and what they assert.
- Explains the Vitest-vs-Jest difference *once* (up front, in the merged block): "asserted below via the DOM `disabled` property — Vitest has no `toBeDisabled` matcher; that's a Jest matcher."
- The "force a click that real users couldn't perform" comment on the `fireEvent.click(runAgainButton)` line is now a comment on the line that does the work, not a separate block.
- The 50ms `setTimeout(resolve)` is now a 0-tick yield (`setTimeout(resolve, 0)`) with a comment explaining why 0 ticks is enough: "the handler is fully sync on the success path: guard → reset → `mutateAsync` returns a promise that never resolves."

The test's prose-to-code ratio is now lower; a future reader sees the contract once, then the assertions.

---



## 5. NIT items not applied

The reviews surfaced 7 NITs. Of those:

- **NIT-1 (mock import order)**: not changed. The current order works.
- **NIT-2 (extract `useTestAlert` hook)**: not done. The dialog is well under the size guideline.
- **NIT-3 (hook re-creation on every render)**: not done. React Query handles hook identity changes correctly.
- **NIT-4 (e2e docstring trim)**: applied — see §2.9.
- **NIT-5 (extract `Results` to its own file)**: not done. The file is well under the 300-line guideline.
- **NIT-6 (top-of-file schema note)**: not done. The `TestAlertHit` JSDoc is sufficient.
- **NIT-7 (AlertsPageBody "Wave 6 banner" comment)**: applied — see §2.8.
- **NIT-8 (`TestAlertDialogProps` export)**: applied — see §2.7.
- **NIT-9 (page.tsx wave-tagged JSDoc)**: applied — see §2.8.
- **NIT-1 from second review (wave-tag in dialog JSDoc)**: applied — see §3.6.
- **NIT-2 from second review (e2e `getByLabel`)**: applied — see §3.3.
- **NIT-3 from second review (complementary "1 events" test)**: deferred — see §1.3.
- **NIT-4 from second review (e2e docstring)**: already applied in revision 1.
- **NIT-5 from second review (page copy fix)**: applied — see §3.4.
- **NIT-6 from second review (page.tsx wave-tag)**: already applied in revision 1.
- **NIT-7 from second review (caption user-facing phrasing)**: applied — see §3.5.
- **NIT-8 from second review (page.tsx single inline line)**: applied — see §3.4.
- **NIT-9 from second review (test `getByText` for chip)**: already correct in revision 1.
- **NIT-10 from second review (`vi.mock` ESM note)**: deferred — see §1.4.
- **NIT-11 from second review (AlertEditorDialog copy)**: not changed. Out of scope.
- **NIT-1 from third review (`runAgain` empty `catch {}` ESLint disable)**: deferred — see §1.6.
- **NIT-2 from third review (`runAgain` `reset()` before `mutateAsync` idle flash)**: deferred — see §1.7.
- **NIT-3 from third review (multi-hit test secondary text assertion)**: deferred — see §1.8.
- **NIT-4 from third review (empty-state caption assertion)**: deferred — see §1.8.
- **NIT-5 from third review (mvp-checklist cross-reference stability)**: deferred — see §1.9.
- **NIT-6 from third review (page.tsx `{" "}` `react/jsx-curly-brace-presence`)**: deferred — see §1.10.
- **NIT-7 from third review (Wave 6 banner copy vs. fresh-user context)**: deferred — see §1.11.
- **NIT-8 from third review (e2e `^email$` anchor fragility)**: deferred — see §1.12.
- **NIT-9 from third review (JSDoc Concurrency section duplication)**: deferred — see §1.13.
- **NIT-10 from third review (e2e `.or()` pattern)**: applied — the pattern was already correct, no change.
- **NIT-1 from fourth review (test comment inconsistency `toBeDisabled` vs DOM property)**: applied — see §4.5.
- **NIT-2 from fourth review (two comment blocks in race-fix test)**: applied — see §4.5 (merged).
- **NIT-3 from fourth review (top-of-file JSDoc vs inline comment)**: deferred — see §1.14.
- **NIT-4 from fourth review (error message `emailSource` line + "check" line)**: deferred — see §1.15.
- **NIT-5 from fourth review (`waitForURL` throw-vs-resolve explanation)**: deferred — see §1.16.
- **NIT-6 from fourth review (50ms `setTimeout` smell)**: addressed — see §4.5 (changed to 0-tick yield with a comment).
- **NIT-7 from fourth review (`fireEvent.click` "bypass" comment)**: addressed — see §4.5.
- **NIT-8 from fourth review (DEFAULT_DEV_PASSWORD placeholder comment)**: deferred — see §1.20.
- **NIT-9 from fourth review (`getByRole("alert")` ambiguity)**: deferred — see §1.21.
- **NIT-10 from fourth review (`getByLabel(/^email$/i)` fragility)**: deferred — see §1.22.
- **NIT-11 from fourth review (e2e `.or()` pattern extraction)**: deferred — see §1.23.

---

## 6. Tripwires honored

- **Default to Server Components.** The dialog is `'use client'`; the page is unchanged.
- **No raw `fetch` in production code.** The dialog uses the React Query mutation hook.
- **No barrel files.** Direct imports.
- **No `any` / no `export default`.** The `TestAlertDialogProps` type is now file-local.
- **A11y.** The empty-payload chip is `<Chip size="small" label="empty payload" />` — MUI's Chip renders a semantic element with the label as the accessible name. The caption text alongside it is plain `<Typography variant="caption">` which inherits the list item's semantics.
- **MUI v9 conventions.** Long-form imports; `sx` for dynamic styles; no inline `style={{}}`.
- **Tests pin behavior, not implementation.** The race-fix test now pins both the UX-layer defense (`disabled` prop) and the data-layer defense (handler's `isPending` guard). A refactor that drops either defense is caught.

---

## 7. Verify

```bash
pnpm test        # 85 passed, 0 failed
pnpm typecheck   # clean
pnpm build       # 7 routes generated
pnpm test:e2e --grep "test alert"  # graceful skip if dev stack isn't up; runs against env vars or dev defaults
```

> Revision history: revision 1 = 74→84 (9 new test cases), revision 2 = 84→85 (race-fix test), revision 3 = 85→85 (changes in the e2e spec + the race-fix test's secondary assertion), revision 4 = 85→85 (comment refactor in the race-fix test; no test count change, no code change, just a clearer JSDoc-style block).

---

## 8. What was reviewed but the original review-of-fixes handoff should track

These are not wave-6 work but the reviews surfaced them:

- **Wave 5 §2.4** — `IDbContextFactory<>` DI gap. Still blocks wave 11 E2E. Still open.
- **Wave 5 §2.3** — `dev:up` / `dev:reset-db` migration wiring. Still deferred. The `MigrationHostedService` follow-up is a wave-10 candidate.
- **Wave 4 §2.3** — MailKit/MimeKit NU1902 advisories. Still deferred to wave 8.
- **Wave 3 "Open question"** — `EmailVerification.Token` and `PasswordResetToken.Token` plaintext storage. Still open. Argon2id or HMAC-SHA256 pre-wave-8.

The frontend has no remaining items from this wave. The deferred items in §1 (1.1 `aria-live`, 1.2 `autoFocus` only on first open, 1.3 complementary plural test, 1.4 `vi.mock` ESM note, 1.5 lint config, 1.6 empty-`catch` ESLint disable, 1.7 `reset()` idle flash, 1.8 secondary text assertions, 1.9 mvp-checklist cross-reference, 1.10 `react/jsx-curly-brace-presence`, 1.11 Wave 6 banner copy, 1.12 `^email$` anchor fragility, 1.13 JSDoc Concurrency duplication, 1.14 e2e JSDoc duplication, 1.15 e2e error message, 1.16 `waitForURL` throw explanation, 1.17 `setTimeout` smell, 1.18 `fireEvent.click` comment, 1.19 NIT-1, 1.20 NIT-2, 1.21 NIT-3, 1.22 NIT-4, 1.23 NIT-5) are all wave-11 polish items.
