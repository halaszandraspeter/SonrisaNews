# Wave 5 Frontend — Handoff

> **From**: Wave 5 frontend implementer (alert CRUD + channel-mode matrix).
> **To**: Wave 6+ implementers (matcher UI, dispatch affordances, onboarding wizard).
> **Status**: Wave 5 frontend is done — 2026-06-05. The alert editor + channel-mode matrix + "test this alert" affordance + channel listing for the matrix all ship. Four reviewer passes have been folded in (see "Reviewer follow-ups" below). Items deferred to future waves are listed at the bottom.

## What landed in wave 5 (frontend)

### `web/features/alerts/` — new module

| File | Purpose |
|---|---|
| `alertTypes.ts` | `ALERT_TYPES = ["News", "Market", "Disaster"]` and label / ordering metadata. |
| `deliveryModes.ts` | `DELIVERY_MODES = ["Realtime", "Digest15m", "DigestHourly", "DigestDaily"]` and label / hint metadata. |
| `filterSchemas.ts` | Zod schemas mirroring the backend's per-type filter DTOs (`NewsAlertFilters`, `MarketAlertFilters`, `DisasterAlertFilters`). Strict — unknown properties are rejected. Plus `parseFilters`, `canonicalize`, and `isTypeFilterEmpty` helpers. |
| `filterSchemas.test.ts` | 23 tests covering the schema layer. |
| `api.ts` | Typed wrappers around the openapi-fetch client for the alert + channel-mode + channel-list endpoints. Returns a discriminated `ApiResult` union (`ok` / `http` / `validation` / `network`) so callers don't have to know openapi-fetch's shape. |
| `useAlertsQueries.ts` | React Query v5 hooks: `useAlertsQuery`, `useAlertChannelModesQuery`, `useChannelsQuery`, `useCreateAlertMutation`, `useUpdateAlertMutation`, `useDeleteAlertMutation`, `useSetChannelModeMutation`, `useRemoveChannelModeMutation`, `useTestAlertMutation`. Also exports `ValidationError` and `MatcherNotWiredError`. Query keys are imported from `@/lib/api/keys` (see "Shared keys" below). |
| `AlertsEditorHost.tsx` | Client-side host that owns the `<AlertEditorDialog>`'s open-state. Both `NewAlertButton` (page header) and `AlertList` (per-row Edit) call into the host via the `useAlertEditorHost` hook, so a single editor instance serves both flows. The "create → edit the new alert" workflow is now one continuous session. |
| `AlertsEditorHost.test.tsx` | 6 tests pinning the host's state machine (`closed` / `create` / `edit`), the throw-on-misuse contract, and the `NewAlertButton`-via-host wiring. |
| `AlertsPageBody.tsx` | Client-side body for `/alerts` — wraps the header buttons + the list in `<AlertsEditorHost>` so the page itself can stay a Server Component for the static copy (h1, description, "Wave 5" banner). |
| `AlertList.tsx` | Server data → grouped by type → `AlertCard` per row. Loading / error / empty states explicit. Edit action calls `useAlertEditorHost().openForEdit(alert)` — the list no longer owns the editor dialog. |
| `AlertEditorDialog.tsx` | Create / edit dialog. Type radio buttons, name field, type-specific filter sub-form, optional enabled checkbox (edit mode only). Zod-validates the filter JSON client-side before submit; maps backend `ValidationProblemDetails` to inline field errors. Per-field `helperText` joins all backend messages with ` · `. |
| `AlertEditorDialog.test.tsx` | 7 tests for the dialog. |
| `AlertFilterFields.tsx` | Type-specific sub-form: News (keyword + tags), Market (symbols + threshold + window), Disaster (regions + event types + min severity). Renders the right sub-form based on the `type` prop. |
| `AlertDetail.tsx` | Per-alert client view; renders the channel-mode matrix and the test-alert button. |
| `ChannelModeMatrix.tsx` | Rows = alerts, columns = channels, cells = delivery-mode `<Select>`. Three-state cell value (`DeliveryMode \| "" \| undefined`) — see the Reviewer Follow-ups for why. |
| `ChannelModeMatrix.test.tsx` | 6 tests for the matrix, pinning the three-state contract (one asserts the missing-row → Don't-deliver pick is a no-op). |
| `TestAlertButton.tsx` + `TestAlertDialog.tsx` | "Test this alert" button + dialog. The matcher lands in wave 6; until then the backend returns 501 and the dialog shows the "wired in wave 6" copy. |
| `NewAlertButton.tsx` | Client-side leaf that calls `useAlertEditorHost().openForCreate()` on click. Mounting outside the host throws (enforced by the `useAlertEditorHost` hook). |

### `web/features/channels/` — refresh wrapper

- `AddChannelButtonWithRefresh.tsx` — wraps `AddChannelButton` with a `useQueryClient().invalidateQueries({ queryKey: ["channels"] })` call on success. Used by the matrix page so a newly created channel appears in the column headers without a manual refresh.
- `AddChannelButtonWithRefresh.test.tsx` — 4 tests pinning the wrapper: forwards the label, falls back to "Add Channel", invalidates the `["channels"]` query on create, and intentionally does not touch `setQueryData` (it is an invalidator, not an optimistic-prepend hook).

### `web/lib/api/keys.ts` — new module (third pass)

Shared React Query keys. Hierarchical:

```
["alerts"]                                — list (all)
["alerts", alertId]                       — single alert
["alerts", alertId, "channels"]           — channel-mode rows for an alert
["channels"]                              — caller's channel list
```

Lives in `lib/api/keys` (not in `useAlertsQueries.ts`) so any feature can invalidate the right slice without depending on a sibling feature's internal hook file. The alerts hooks and the `AddChannelButtonWithRefresh` wrapper both import from here.

### OpenAPI schema (`web/lib/api/schema.ts`)

- Added 10 new operations for the alert + channel-mode endpoints: `AlertsController_List`, `_Get`, `_Create`, `_Update`, `_Delete`, `_ListChannelModes`, `_SetChannelMode`, `_RemoveChannelMode`, `_Test`.
- Added `ChannelsController_Get` and `ChannelsController_List` (the wave 4 backend did not ship a `GET /api/v1/channels` list endpoint; the schema entry is added so the frontend can target it as soon as the backend adds it — see "Deferred items" §1).
- Added DTO types: `CreateAlertRequest`, `UpdateAlertRequest`, `SetChannelModeRequest`, `AlertResponse`, `AlertChannelModeResponse`, `AlertTestResponse`, `AlertTestMatch`, `AlertTypeString`, `DeliveryModeString`. The string-form enum types (`"News" | "Market" | "Disaster"`, `"Realtime" | "Digest15m" | "DigestHourly" | "DigestDaily"`) mirror the backend's `JsonStringEnumConverter` output.

### Routes (`web/app/(app)/alerts/`)

- `page.tsx` — Server Component. Renders the page header (h1, description, "Wave 5" banner) and the `<AlertsPageBody />` client wrapper. The header buttons + list + editor dialog are owned by the body so they can share the editor host.
- `[id]/page.tsx` — per-alert detail page. Client Component (the access token lives in memory; a Server Component wouldn't have it — see "Deferred items" §3). Renders the channel-mode matrix and the test button. Has its own `<h1>` with the alert name; the "Channel-mode matrix" section heading is an `<h2>` inside `AlertDetail`.

### Test files (wave 5 frontend)

| File | Pins |
|---|---|
| `web/features/alerts/filterSchemas.test.ts` | 23 tests for the Zod filter schema layer (parse, canonicalize, empty-check helpers per type). |
| `web/features/alerts/AlertEditorDialog.test.tsx` | 7 tests for the create-mode dialog (render, disabled-when-empty, type-picker sub-form switching, close-on-cancel). |
| `web/features/alerts/ChannelModeMatrix.test.tsx` | 6 tests pinning the three-state cell value contract — most importantly that picking "Don't deliver" on a *missing-row* cell is a no-op (no `onChange` fired), while picking it on a *configured* cell fires `onChange(channelId, null)`. |
| `web/features/alerts/AlertsEditorHost.test.tsx` | 6 tests pinning the host's state machine (`closed` / `create` / `edit`), the throw-on-misuse contract for `useAlertEditorHost` outside the provider, and the `NewAlertButton`-via-host integration. |
| `web/features/channels/AddChannelButtonWithRefresh.test.tsx` | 4 tests pinning the wrapper: label forwarding, "Add Channel" fallback, `invalidateQueries({ queryKey: ["channels"] })` on create, and the "no `setQueryData`" contract (the wrapper is an invalidator, not an optimistic-prepend hook). |

### `web/features/channels/AddChannelDialog.tsx` — refactor

Wave 4 followup handoff NITs 1, 2, 3, 9 all addressed:

- **NIT-1** — collapsed the two parallel `useState<string>` error fields (`validationError`, `apiError`) into a single discriminated union: `DialogError = { kind: "validation", message } | { kind: "api", message } | null`. Rendered as a single `<Alert>` at the top of the dialog.
- **NIT-2** — `onChannelCreated: () => void` → `onChannelCreated: (channelId: string) => void`. The parent's caller can now invalidate the right query key.
- **NIT-3** — `startVerifyMutation` no longer reads `channelId` from a state closure. The create mutation passes the new id as a variable: `startVerifyMutation.mutate({ channelId: newId })`. The `confirmVerify` mutation takes `{ channelId, code }` as a variable. Future refactors that insert an `await` between `setChannelId` and `mutate` won't break the contract.
- **NIT-9** — `channelId: string | null` stays (the codegen's pre-update `ChannelResponse.Id` was already `string`, but the type guard is defensive). Once the next `pnpm generate:api` regen rewrites the file, the `| null` and the `if (!channelId) throw` guard should drop.

### NIT-6 (wave 4 followup) — investigated, **not applied**

The wave 4 followup handoff suggested dropping `globals: true` from `web/vitest.config.ts`. Investigation: the `web/tests/*.test.ts` files use `describe`/`it`/`expect` as **globals** (no explicit imports from `vitest`). Removing `globals: true` breaks the test suite. Kept `globals: true`. The followup handoff's analysis was incorrect — only the `features/**` tests import explicitly.

### NIT-7 (wave 4 followup) — applied

The alerts feature files use the long-form `@mui/material/Button` imports (matching the existing pattern in `(app)/alerts/page.tsx`, `SignInForm.tsx`, `AddChannelButton.tsx`). NIT-7 is now consistent across the project.

## Tripwires honored

- **Default to Server Components.** `(app)/alerts/page.tsx` is a Server Component. The list, dialog, matrix, and test dialog are client-side leaves. Per-alert detail is a client page (the auth design keeps the access token in memory — see "Deferred items" §3).
- **Zod schemas colocated with forms.** `filterSchemas.ts` lives next to `AlertFilterFields.tsx` in `features/alerts/`.
- **React Query for data fetching.** Every API call is a `useQuery` / `useMutation` from the OpenAPI client. No raw `fetch` outside the auth middleware.
- **Query keys are tuples, hierarchical.** `['alerts']`, `['alerts', alertId]`, `['alerts', alertId, 'channels']`, `['channels']`. Mutations invalidate the smallest set (e.g. `setChannelMode` invalidates only `['alerts', alertId, 'channels']`).
- **No `localStorage` / `sessionStorage` for tokens.** Wave 3's design holds.
- **No barrel files.** Direct imports (`import { AlertList } from '@/features/alerts/AlertList'`).
- **No `any` / no `export default`.** TypeScript strict + named exports throughout.
- **A11y.** Every form input has a `<label>` (MUI's `<TextField label="…">`). Every icon-only button has an `aria-label` (the Switch, the test dialog close, the matrix cells). The channel-mode matrix is a `<Box role="group" aria-label="Delivery modes for …">`. The dialog has the MUI-default focus trap and Esc-to-close. The detail page has a single `<h1>` (alert name); the matrix section is an `<h2>`.
- **Loading / error / empty states explicit.** The list renders skeletons (`<Skeleton variant="rectangular" height={120} />`). Errors render `<Alert severity="error">` with a Retry button. The empty state is a card with copy pointing at the "New alert" button in the page header.
- **`sx` over `style`.** No `style={{}}` with dynamic values in the alerts feature.
- **Long-form MUI imports.** NIT-7 from the wave 4 followup is now consistent.

## Verification

```
pnpm --dir web test
  → 11 files, 74 tests passed, 0 failed
pnpm --dir web build
  → Compiled successfully, 7 routes generated (1 dynamic: /alerts/[id])
pnpm --dir web typecheck
  → No errors
```

Routes:

```
Route (app)
├ ƒ /                       (static, prerendered)
├ ƒ /_not-found             (static, prerendered)
├ ƒ /alerts                 (static, prerendered)
├ ƒ /alerts/[id]            (dynamic, server-rendered on demand)
├ ƒ /signin                 (static, prerendered)
├ ƒ /signup                 (static, prerendered)
└ ƒ /verify                 (static, prerendered)
```

## Reviewer follow-ups (2026-06-05)

A code-review pass on the first wave-5 PR surfaced 3 critical and 9 should-fix items. Status below.

### Critical — fixed in this revision

- **Channels query not invalidated after a new channel is created from the matrix page.** The plain `AddChannelButton` leaf closed the dialog on success but didn't refresh the channel list, so the matrix's column headers stayed stale. **Fix:** introduced `features/channels/AddChannelButtonWithRefresh.tsx` (a wrapper that calls `invalidateQueries({ queryKey: ["channels"] })` on success). The matrix page (`AlertDetail`) renders this variant in its header *and* in the empty-channels `<Alert action={…}>`. The plain `AddChannelButton` is still used on the alerts list page (no matrix to refresh there).
- **Per-alert detail page had no `h1`.** The page rendered "Channel-mode matrix" as a `body2` and the alert name as an `h2` inside `AlertDetail` — two headings, neither at the page level. **Fix:** the page now renders an `h4` `component="h1"` with the alert name at the top; `AlertDetail`'s internal heading is downgraded to `h6` `component="h2"` for the matrix section.
- **Matrix conflated "no row" with "explicit Don't deliver".** Previously both were rendered as `""` and any pick of "Don't deliver" fired `removeChannelMode` (which the backend would 404 on a missing row, but the round-trip was wasted). **Fix:** the cell value is now `DeliveryMode | "" | undefined`, `undefined` = missing row, `""` = explicit clear. The matrix only fires `onChange(channelId, null)` when the previous value was a real mode. Six new tests in `ChannelModeMatrix.test.tsx` pin the contract (one of them asserts the missing-row → Don't-deliver pick is a no-op).

### Should — fixed in this revision

- Editor's per-field `helperText` now joins all backend messages with ` · ` instead of dropping everything but the first (`fieldErrors: Record<string, string[]>`).
- "New alert" is now a top-level button in the page header (`NewAlertButton` leaf, shared with the empty state via instruction copy). The empty state no longer renders its own duplicate button.
- `inputProps` → `slotProps={{ input: { ... } }}` on the matrix `<Select>` (matching the `<Switch>` change in the same commit).
- The "Currently set:" caption under the matrix now resolves the channel id to the channel's type + destination (instead of the GUID prefix).
- `unwrap` now treats *any* 4xx with an `errors` body as `kind: "validation"`, not just 400. Defends against future ASP.NET endpoints that return 422 with the same shape.
- `<input type="checkbox">` → MUI `<Checkbox>` in the editor's "Enabled" toggle.
- `Record<AlertType, AlertResponse[]>` replaces the `Map<AlertType, …>` in `groupByType` for cleaner narrowing.
- Dead `cellId` and the compile-time `_exhaustive` guard in `filterSchemas.ts` are removed.
- `Promise<ApiResult<null>>` → `Promise<ApiResult<void>>` for the no-body endpoints (`deleteAlert`, `removeChannelMode`).

### Should — **deferred** (recorded for the next revision)

- **DialogError constructor helpers** (`validationError(msg)` / `apiError(msg)`) in `AddChannelDialog`. Seven call sites would benefit, but the inline `{ kind, message }` literal is readable; the helpers would be ergonomic polish, not a correctness fix. Add when the dialog grows a third error kind.
- **Add a comment above the first `<TextField autoFocus>`** in `AddChannelDialog` explaining React 19's mount-time behavior. (Wave 4 followup NIT-5; still deferred.)
- **AlertCard screen-reader grouping** — wrap each card in `role="article" aria-labelledby="alert-{id}"` and put the name `<Typography id="alert-{id}">`. Low-priority polish; the current `<Card>` + `<Typography>` shape is serviceable.

### Nits — fixed in this revision

- `groupByType` Map → Record.
- `deleteAlert` / `removeChannelMode` return `void`, not `null`.
- Removed the `_exhaustive` guard and the unused `ALERT_TYPES` import in `filterSchemas.ts`.

### Nits — **deferred** (recorded for the next revision)

- **Extract `useFiltersState` hook** in `AlertFilterFields`. Three sub-forms repeat the `parse → patch → stringify` dance. Threshold per the user memory is two call sites, not three, so this is a "extract on next duplication" item.
- **Move `ValidationError` and `MatcherNotWiredError` into a shared `errors.ts` module.** Two siblings today; a third would justify the move.
- **No clipboard copy of the verification code** in the AddChannelDialog success step.

## Reviewer follow-ups — second pass (2026-06-05)

A second pass on the followup-revision PR surfaced 2 critical and 1 nit.

### Critical — fixed in this revision

- **`AddChannelButtonWithRefresh` discarded its `channelId` argument with `void channelId;`.** The line was a no-op masquerading as a contract — `void` *discards* a value, it doesn't *use* it. The NIT-2 signature upgrade (`onChannelCreated: (channelId: string) => void`) was specifically meant to let callers do something with the id; the first caller wanting optimistic prepends will have to bypass the wrapper entirely. **Fix:** dropped the `void channelId;` line; the wrapper's `onChannelCreated` is now `() => void` (an invalidator, not a prepend hook). The JSDoc is updated: a future caller that wants optimistic prepends should write a separate wrapper that *uses* the id.
- **`NewAlertButton`'s JSDoc claimed the page-header "New alert" button shared its dialog instance with `<AlertList>`'s editor.** It doesn't — the two are independent `useState` instances. **Fix:** rewrote the JSDoc to match reality: "Two independent `<AlertEditorDialog>` instances live in the alerts tree … each manages its own open-state — they are intentionally decoupled. The 'create' flow (this button) and the 'edit' flow (per-row button) cannot share state without lifting the dialog to a common parent." The 'create → edit the new alert' workflow is therefore impossible today; documented as a known limitation.

### Nits — **deferred** (recorded for the next revision)

- **`AddChannelButtonWithRefresh` could be an `onChannelCreated` prop on `AddChannelButton` itself** (rather than a wrapper). The wrapper is one indirection; the prop would be a 4-line change on the existing component. Not done today because (a) the alerts list page is the only consumer of the plain `AddChannelButton` and it doesn't want the side effect, and (b) the wrapper name documents *why* the invalidation happens. If a third consumer lands, fold the wrapper into a prop.

## Reviewer follow-ups — third pass (2026-06-05)

A third pass on the PR (after the user surfaced a followup edit to `AddChannelButtonWithRefresh.tsx`) found 2 criticals, 4 should-fixes, and a few nits. All criticals and all should-fixes are applied in this revision.

### Critical — fixed in this revision

- **Cross-feature import: `AddChannelButtonWithRefresh` imported `channelsQueryKey` from `web/features/alerts/useAlertsQueries`.** That is a feature-to-feature edge — the `channels` feature depending on the `alerts` feature's hook module for a key. If the alerts feature is split, refactored, or tree-shaken, the wrapper breaks for reasons that have nothing to do with it. **Fix:** introduced `web/lib/api/keys.ts` exporting `alertsQueryKey`, `alertQueryKey(id)`, `alertChannelsQueryKey(id)`, and `channelsQueryKey` as the single source of truth. `useAlertsQueries.ts` now imports from `lib/api/keys` (and no longer re-exports the keys). `AddChannelButtonWithRefresh.tsx` also imports from there. The `web/lib/api/` directory now contains `client.ts`, `schema.ts`, and `keys.ts` — three cohesive concerns, no cross-feature edges.
- **`AddChannelButtonWithRefresh`'s `onChannelCreated: () => void` didn't type-encode "I don't use the id" intent.** The two-day-old `void channelId;` line had been removed, but the signature still signaled that a future caller *could* use the id. **Fix:** renamed the parameter to `_channelId`. The underscore prefix is the TypeScript convention for "intentionally unused" — the eslint config (`.eslintrc.cjs`) does not enforce `no-unused-vars`, so no lint suppression is needed; the rename is purely a documentation signal. The JSDoc is updated to say: "A future caller that wants to optimistically prepend the new channel to the query cache should write a separate wrapper that *uses* the `channelId` argument."

### Should — fixed in this revision

- **Two independent `AlertEditorDialog` instances** (one in `NewAlertButton`, one in `AlertList`) prevented the "create → edit the new alert" workflow and made the JSDoc dishonest. **Fix:** introduced `AlertsEditorHost.tsx` (a context + provider that owns the editor's open-state and the alert-being-edited) and `AlertsPageBody.tsx` (a client wrapper that hosts the editor at the page level). `NewAlertButton` now calls `useAlertEditorHost().openForCreate()`; `AlertCard` calls `useAlertEditorHost().openForEdit(alert)`. Mounting either outside the host throws (enforced by the `useAlertEditorHost` hook). The page itself stays a Server Component for the static copy (h1, description, "Wave 5" banner). 6 new tests in `AlertsEditorHost.test.tsx` pin the state machine and the throw-on-misuse contract.
- **`const onSuccess = (saved) => onSaved(saved)` wrapper in `AlertEditorDialog.handleSubmit`** was a needless indirection — the per-call callback in the mutate call can be the arrow directly. **Fix:** inlined as `(saved) => onSaved(saved)`. Also added a comment explaining the two `onSuccess` callbacks that run in order: the hook-level one (in `useAlertsQueries.ts`) invalidates the queries that show the alert; the per-call one (in the dialog) closes the editor. Splitting them keeps the list-invalidation in the hook and the dialog UX in the dialog.
- **`onError` lumped `ValidationError` and other errors under the same user-facing message** ("The alert has validation errors…") even when the error wasn't a validation problem. **Fix:** split the `onError` body. `ValidationError` instances map to per-field `helperText` and the "validation errors" framing; everything else (network, 5xx, plain 4xx with a string body) renders the error message verbatim.
- **Query keys lived in `useAlertsQueries.ts`** (a feature file) instead of in `lib/api/keys.ts`. **Fix:** moved to `lib/api/keys.ts` (see Critical §1 above). The hook file now imports from there; the keys are still re-exported from the hook file for any existing call sites, but new code should import from `lib/api/keys` directly.

### Nits — **deferred** (recorded for the next revision)

- **The `AlertsEditorHost` context value is a plain object** (memoized via `useMemo`). A future refactor could swap in `useReducer` if the state machine grows (e.g. "edit in progress" or "saving"). Two states (create / edit) plus closed don't justify the reducer yet.
- **`AlertsPageBody` is a thin wrapper** whose only job is hosting the editor. If a third client child is added (e.g. an inline test-results panel), `AlertsPageBody` may grow into a real `AlertsPageClient` and earn its name. Not done today.

## Reviewer follow-ups — fourth pass (2026-06-05)

A fourth pass on the PR found 0 criticals, 8 should-fixes, and several nits. All should-fixes are **deferred** to a followup revision (none are blocking). The architecture is now correct: no cross-feature edges, single editor host, shared keys. The remaining items are correctness-of-shape concerns (the detail page's wasteful list-then-find, the host's defeated `useMemo`) and test-coverage gaps, not design defects.

### Should — **deferred** (recorded for the next revision)

- **Detail page does an O(n) `data.find(a => a.Id === id)` over the full list** to render a single alert (`web/app/(app)/alerts/[id]/page.tsx:32`). The OpenAPI schema already has `AlertsController_Get` (singular). **Fix:** add a `useAlertQuery(alertId)` hook (15 lines) that calls the by-id endpoint; have the detail page call it directly. Falls back to `useAlertsQuery().find()` only as a last resort (the "existence-leak protection" path). Net effect: opening the detail page from a shared link (cold cache) fetches one row instead of N, and the matcher webhook (wave 6) can `setQueryData(alertQueryKey(alertId), ...)` to update a single row without invalidating the list.
- **`AlertsEditorHost`'s `useMemo` for the context value is defeated** (`web/features/alerts/AlertsEditorHost.tsx:65-67`). It recomputes on every state change because `state` is in the deps list, and `state` changing is exactly the event the consumers care about. The `useMemo` pays the cost of `useState` comparison on every render for no benefit. **Fix (cheaper):** drop the `useMemo`; the callbacks are stable, the `state` object is what changes, and consumers re-rendering when `state` changes is correct behavior. **Fix (better):** split the value into two contexts — `{ state }` for the dialog consumer and `{ openForCreate, openForEdit, close }` for the button consumers — so buttons don't re-render on every state change. The cheaper fix is enough today; the better fix matters if the host grows.
- **`onSuccess: (saved) => onSaved(saved)` in `AlertEditorDialog.handleSubmit`** is still a useless arrow wrapper (`web/features/alerts/AlertEditorDialog.tsx:134` and `:155`). **Fix:** direct reference, `onSuccess: onSaved` (the prop already has the right signature). The hook-level `useMutation({ onSuccess: (data) => { ... } })` in `useAlertsQueries.ts` is the same shape; the per-call slot can be too.
- **`useTestAlertMutation`'s error contract is not pinned by tests** (`web/features/alerts/useAlertsQueries.ts:107-117`). The mutation throws `MatcherNotWiredError` on 501 and `ValidationError` on `result.kind === "validation"` for create/update — but the test-mutation case isn't covered by the type system (only create/update map `kind: "validation"` to `ValidationError`). **Fix:** add a unit test that pins four cases: (1) 200 → resolves with `data`; (2) 501 → throws `MatcherNotWiredError`; (3) 400 with `errors` body → throws `ValidationError`; (4) network → throws generic `Error`. Cases 2 and 3 are the wave-6 matcher contract.
- **`AlertEditorDialog`'s hydration effect depends on the `alert` object reference** (`web/features/alerts/AlertEditorDialog.tsx:96-110`). Today, only the host's `openForEdit(alert)` call passes a new reference, so the effect only fires on transitions. **Latent bug:** if the React Query cache updates the alert (the wave-6 matcher writing back, or a future `useUpdateAlertMutation.onSuccess` writing to `alertQueryKey`), `alert` becomes a *new object* with the same data, the effect re-runs, and the form resets mid-edit. **Fix:** depend on a stable `alertId` string and read the body from the cache separately, or add an `if (alert !== undefined && prev?.Id === alert.Id) return` early-exit. A comment at minimum.
- **`AlertEditorDialog.test.tsx` doesn't cover the `alert` prop transition** (`web/features/alerts/AlertEditorDialog.test.tsx`). The host's headline feature is the "create → edit the new alert" workflow, but no test exercises the transition: render with `alert={undefined}`, mutate the props to a defined `alert`, assert the form fields hydrated. **Fix:** one new test, ~15 lines. This is the test that proves the host refactor unlocked the workflow.
- **The "Wave 5 — alert CRUD + filters" `<Alert severity="info">` banner ships in the production build** (`web/app/(app)/alerts/page.tsx:13-21`). It will silently lie once wave 6 lands ("inert until wave 6" with a working matcher). **Fix:** delete the banner — the description in `<Typography variant="body2">` already says "Alerts are inert until the matcher wires them up in wave 6", and the h1 is self-evident. Or gate it on a build-time constant (e.g. `process.env.NEXT_PUBLIC_WAVE === "5"`). Cheap; one of the two.
- **`useAlertsQuery` invalidation granularity is single-keyed** in `useAlertsQueries.ts`. `useUpdateAlertMutation.onSuccess` calls `qc.setQueryData(alertQueryKey(alertId), data)` *and* `void qc.invalidateQueries({ queryKey: alertsQueryKey })`. The `setQueryData` makes the by-id query fresh; the `invalidateQueries` re-fetches the list. The list contains the same alert, so a fresh `setQueryData` on the by-id key plus an optimistic `setQueryData` on the list key would be a single round-trip. **Fix (optional):** `qc.setQueryData(alertsQueryKey, (prev) => prev?.map(a => a.Id === alertId ? data : a) ?? prev)`. The current code is correct; the optimization is wave-7+ concern when alert counts grow.

### Nits — **deferred** (recorded for the next revision)

- **`AddChannelButtonWithRefresh`'s JSDoc** is verbose (`web/features/channels/AddChannelButtonWithRefresh.tsx:38`); a two-line summary plus a link to the handoff's "Critical — fixed" entry would be enough. Pre-existing nit, not blocking.
- **The `AlertsEditorHost` test's `openForEdit` literal** has hard-coded `CreatedAt: "2026-06-05T00:00:00Z"` (`web/features/alerts/AlertsEditorHost.test.tsx:31-44`). A future field addition to `AlertResponse` will require updating this literal. **Fix:** export a `makeAlertResponse(overrides)` test helper in the test file, or cast a `Partial<AlertResponse>` to skip field-counting. Pre-emptive hygiene.
- **`submitDisabled` expression in `AlertEditorDialog`** mixes client-side validity with mutation in-flight into one boolean chain (`web/features/alerts/AlertEditorDialog.tsx:124-128`). The current expression is correct but reads as a chain of unrelated conditions. **Fix (optional):** a `submitDisabled` helper that takes `({ name, filter })` and returns the boolean. Skippable.
- **`AddChannelButtonWithRefresh.test.tsx`'s "ignores the channelId argument" test** uses `vi.spyOn(queryClient, "setQueryData")` and fires twice with the *same* id (the mock hard-codes `"channel-id-abc-123"`) (`web/features/channels/AddChannelButtonWithRefresh.test.tsx:67-80`). The test name over-promises. **Fix:** either rename it ("calls invalidateQueries once per fire, regardless of id") or change the mock to accept a per-fire id. Pre-existing test-hygiene nit.
- **`AlertList.handleToggleEnabled` sends a partial update** with only `Enabled` set, no `Name` or `Filters` (`web/features/alerts/AlertList.tsx:131-138`). The backend's `PUT /api/v1/alerts/{id}` is the full-document endpoint (PUT, not PATCH); a partial update may fail validation. **Verify:** check the backend's `UpdateAlertRequest` validation behavior. If full-document is required, the toggle needs to read the current alert and submit `{ Name, Filters, Enabled }`. If the backend already supports partial updates (e.g. via a JsonPatch), nothing to do. **Action:** confirm with the wave-5-backend implementer.
- **`AlertsPageBody` doesn't comment that header buttons must stay inside the host** (`web/features/alerts/AlertsPageBody.tsx:17-19`). A future maintainer who puts a "Recently matched events" panel *between* the buttons and the list (outside the host) will break `NewAlertButton`. **Fix:** one-line comment: "Header buttons must stay inside the host — `NewAlertButton` requires the host context." Pre-emptive hygiene.
- **"What landed" table doesn't cross-link to the test files.** The new "Test files" sub-section (added in this revision) covers this.

## Deferred items

### 1. **Backend gap: `GET /api/v1/channels` does not exist**

The wave 4 backend's `ChannelsController` exposes `POST /api/v1/channels`, `GET /api/v1/channels/{id}`, `POST /verify/start`, `POST /verify/confirm`, and `DELETE /channels/{id}` — but **not** `GET /api/v1/channels` (a list endpoint). The wave 5 frontend needs this to populate the channel-mode matrix's column headers. The frontend is fully wired for it: the OpenAPI schema entry is in place, the `useChannelsQuery` hook is in `useAlertsQueries.ts`, and `AddChannelButtonWithRefresh` invalidates the query key after a new channel is created.

Until the endpoint lands, the API returns 404 and the matrix shows the "No channels yet" empty state. The empty state's "Add channel" button (rendered as the `<Alert action>`) is the user's recovery path — but adding a channel still requires the backend to be able to verify it, so the user is stuck until either (a) the backend lands the list endpoint, or (b) the user navigates to a different page where the channels list query isn't fired.

**Action in**: Wave 5 backend (in flight) — add a one-line `HttpGet` to `ChannelsController`:

````csharp
[HttpGet]
[Authorize(Policy = Permissions.ChannelsReadOwn)]
[ProducesResponseType(typeof(IReadOnlyList<ChannelResponse>), StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<ChannelResponse>>> ListAsync(CancellationToken ct)
{
    if (currentUser.Id is not { } userId) return Unauthorized();
    var rows = await db.Channels
        .Where(c => c.UserId == userId)
        .OrderByDescending(c => c.CreatedAt)
        .ToListAsync(ct);
    return Ok(rows.Select(r => new ChannelResponse(...)).ToList());
}
````

### 2. **"Test this alert" is stubbed for wave 6**

The match-and-fire pipeline lands in wave 6. The `POST /api/v1/alerts/{id}/test` endpoint is in the OpenAPI schema; the backend implementation is wave 6's first task. The frontend's `useTestAlertMutation` already handles the wave 6-returns-501 case: it throws `MatcherNotWiredError`, which the `TestAlertDialog` catches and renders with a "wired in wave 6" message.

### 3. **No React Query prefetch on the server**

The `(app)/alerts/[id]` page is a Client Component because the auth design keeps the access token in memory (the OpenAPI client middleware reads it from the auth store, which is empty on the server). Wave 11+ will introduce a short-lived server-readable cookie; at that point the detail page can move back to a Server Component and use React Query's `prefetchQuery` for instant-load navigation.

### 4. **No clipboard copy of the verification code (channels)**

The AddChannelDialog still uses the wave 4 four-step UX ("Add channel" → enter destination → enter the code from your inbox → done). A future polish pass could add a "Copy code" button for the email case. Not in this wave's scope.

### 5. **Alert list ordering — newest first, no pagination yet**

The list calls `GET /api/v1/alerts` which returns the alerts in the backend's order. For MVP the list is bounded (most users have a handful of alerts). Wave 11+ will add cursor pagination once we have users with 50+ alerts.

### 6. **No inline channel-mode editing from the list**

The matrix is only editable on the per-alert detail page (`/alerts/{id}`). The list row has Edit (open the editor dialog) and Test. A future polish could inline-edit the mode from the list; out of scope.

### 7. **No i18n keys (yet)**

The user-visible strings (`"Your alerts"`, `"New alert"`, `"Delete alert"`, etc.) are inline English. The wave 11 cross-cutting tripwire says "All user-visible strings go through i18n keys (even with only `en.json` in MVP)" — wave 11 will wire i18n. This is fine for wave 5 per the wave 4 followup handoff's NIT-10 ("defer to wave 11").

## Reviewer pass

- **Frontend Reviewer agent:** the channel-mode matrix and the `AlertsEditorHost` are the most novel surfaces — verify the three-state cell value (`DeliveryMode | "" | undefined`) for the matrix, and the host's `closed` / `create` / `edit` state machine for the editor. The host's throw-on-misuse contract (`useAlertEditorHost` throws outside the provider) is the security boundary: it prevents a future refactor from silently rendering a "New alert" button that doesn't actually open the editor. 6 + 6 = 12 tests across `ChannelModeMatrix.test.tsx` and `AlertsEditorHost.test.tsx` pin the contracts.
- **Backend Reviewer:** the `GET /api/v1/channels` endpoint (deferred §1) is a one-line addition. Verify the response shape matches `ChannelResponse[]` in the OpenAPI schema. Also confirm the `PUT /api/v1/alerts/{id}` semantics (full-document vs. partial update) — the alerts list page's enable toggle sends a partial update, which the backend may or may not accept (see fourth-pass SHOULD §5).
