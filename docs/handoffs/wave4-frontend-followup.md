# Wave 4 Frontend — Follow-up

> **Status**: Wave 4 frontend shipped 2026-06-05. The SHOULD items in the
> review are addressed in the wave 4 commit. The items below are
> deferred — they don't block this wave's "Verify" but should be
> picked up before or during wave 5.

## Fixed in this wave

- ✅ Page-level `'use client'` removed. `(app)/alerts/page.tsx` is
  back to a Server Component. The new client-side leaf
  `features/channels/AddChannelButton.tsx` owns the dialog open/close
  state. See [.github/instructions/nextjs-react.instructions.md](../instructions/nextjs-react.instructions.md) — "Push `'use client'` to the leaf."
- ✅ `useMutation` declarations reordered top-to-bottom in
  `AddChannelDialog.tsx`. `confirmVerify` → `startVerify` →
  `createChannel`. The file now reads in dependency order. Added a
  comment block above the mutations explaining the closure / batching
  invariant for `setChannelId` + `startVerifyMutation.mutate()`.
- ✅ Removed the `setTimeout(..., 500)` magic number. The success
  step now shows "Channel verified!" + a single "Done" button that
  the user must click. No auto-close. No dual path. Picked one UX.
- ✅ Cleaned up the `DialogActions` rendering. No more dead
  `<div />` placeholder. The structure is now a single ternary
  (success vs. not-success) with Back hidden on the initial step and
  Cancel always present.
- ✅ Added `vi.mock("@/lib/api/client", ...)` to
  `AddChannelDialog.test.tsx`. Future tests that trigger the
  create/start/confirm mutations won't hit the real `openapi-fetch`
  middleware in jsdom.
- ✅ Updated the JSDoc on `(app)/alerts/page.tsx` so the "Server
  Component" claim is true again. Also updated the "wave 4" copy to
  "wave 5" for the full-dashboard note.

## Deferred items (NITs + one SHOULD)

These are recorded here so they don't get lost. None of them are
correctness bugs. The wave 4 "Verify" block (28 unit tests pass,
`pnpm build` green) is satisfied.

### NIT-1 — Discriminated-union error state

**File**: `web/features/channels/AddChannelDialog.tsx:39-44`

Two parallel `useState` strings (`validationError`, `apiError`) that
get cleared in tandem. Collapse into one:

```ts
type DialogError =
  | { kind: "validation"; message: string }
  | { kind: "api"; message: string }
  | null;
const [error, setError] = useState<DialogError>(null);
```

**Action**: pick this up when the dialog grows (wave 5 needs a
`deleteChannel` flow, possibly a `resendCode` flow — both can share
the same error state).

### NIT-2 — `onChannelCreated` should carry the channel ID

**File**: `web/features/channels/AddChannelDialog.tsx:127-130`

`onChannelCreated: () => void` doesn't tell the parent which channel
was just created. The parent's likely future caller is a
`useChannels` query that needs to invalidate / re-fetch.

```ts
interface AddChannelDialogProps {
  open: boolean;
  onClose: () => void;
  onChannelCreated: (channelId: string) => void;
}
```

**Action**: wave 5 — when the dashboard wires up the channel list
(`useQuery({ queryKey: ["channels"] })`), it needs the ID to
optimistically prepend the new channel or invalidate the right query
key.

### NIT-3 — `startVerifyMutation` reads `channelId` from closure

**File**: `web/features/channels/AddChannelDialog.tsx:142-145`

The closure captures `channelId` at the time `mutate()` is called.
The code sets `setChannelId(channel.Id)` *then* calls
`startVerifyMutation.mutate()` in the same `onSuccess` block. Today
this works because React batches the setState and the closure sees
the new value. **This is fragile.** A future refactor that puts an
`await` between the two calls would silently break it.

**Action**: refactor to pass the ID as an argument:

```ts
onSuccess: (channel) => {
  startVerifyMutation.mutate(channel.Id);
}
```

…and read `channelId` from the `variables` in `onSuccess` /
`mutationFn`. Eliminates the closure read.

### NIT-4 — `validationError` set inside `setDestination` onChange

**File**: `web/features/channels/AddChannelDialog.tsx:231-235, 254-258`

The `TextField` onChange clears the validation error on every
keystroke. That's good UX but the side-effect belongs in a
`useEffect([destination])`, not in the onChange handler. If we
add a "blur = revalidate" pattern later, mixing it with onChange
gets confusing.

**Action**: only refactor if the dialog grows a blur-revalidate
behavior. Today it's fine as-is.

### NIT-5 — `TextField` `autoFocus` comment

**File**: `web/features/channels/AddChannelDialog.tsx:225, 248`

Two `<TextField autoFocus>` calls. In React 19, `autoFocus` only
fires on mount, not on every re-render — so when the step changes,
the *new* TextField mounts and gets focus, which is what we want.
But a future reader might think it's a bug.

**Action**: add a one-line comment above the first `autoFocus`:

```ts
// autoFocus triggers on mount; the new TextField mounts when the
// step changes, so focus moves to the new input as expected.
```

### NIT-6 — `vitest.config.ts` `globals: true` is unused

**File**: `web/vitest.config.ts`

`globals: true` enables `describe`/`it`/`expect` as globals, but
every test still imports them explicitly from `vitest`. Drop one or
the other.

**Action**: prefer dropping `globals: true` (matches the existing
test style — explicit imports).

### NIT-7 — MUI long-form vs short-form imports

**Files**: `web/features/channels/AddChannelDialog.tsx`,
`web/features/channels/AddChannelButton.tsx`,
`web/app/(app)/alerts/page.tsx`

`AddChannelDialog` uses `@mui/material` (short-form). The
`AddChannelButton` and `page.tsx` use `@mui/material/Button` etc.
(long-form, tree-shake-friendly). Both work; pick one for the
project.

**Action**: settle on long-form `@mui/material/Button` for the
project (it makes the bundler's job easier). Apply across the
existing files in a small follow-up.

### NIT-8 — `<Box>` import unused if we drop the success layout

**File**: `web/features/channels/AddChannelDialog.tsx:13`

`Box` is used in the success step. If we keep that step, keep the
import. If we ever collapse the success step into a `Typography`
only, drop `Box` from the imports.

**Action**: leave as-is for now. Wave 5 will likely restyle the
success step anyway.

### NIT-9 — `channelId: string | null` is OK but tighten on read

**File**: `web/features/channels/AddChannelDialog.tsx:38, 88, 144`

The `null` branch is theoretically reachable (the `if (!channelId)
throw` guards catch it). But once the OpenAPI codegen
(`pnpm generate:api`) regenerates the schema, `channel.Id` will be
`string` not `string | null` — the codegen output is a
backend-guaranteed non-null. The `null` arm is dead code.

**Action**: drop the `| null` and the `if (!channelId) throw`
guards after the next OpenAPI regen. The DB schema has `Id` as
`Guid` (non-nullable), so the C# `ChannelResponse.Id` will be
`string` (non-null) in the regenerated `schema.ts`.

### NIT-10 — `Zod` error message strings

**File**: `web/features/channels/AddChannelDialog.tsx:21-32`

`"Invalid email address"` and `"Invalid webhook URL"` are
user-facing copy. They should go through i18n (the wave 11 tripwire
"All user-visible strings go through i18n keys"). Today the project
doesn't have i18n wired; the strings will need a `t("...")` wrap
when i18n lands.

**Action**: defer to wave 11. The `nextjs-react.instructions.md`
doesn't enforce i18n yet (it lives in the cross-cutting tripwires
in `mvp-checklist.md` §3, item 8), so this is fine for now.

## Verification

```
pnpm --dir web test
  → 6 files, 28 tests passed, 0 failed
pnpm --dir web build
  → Compiled successfully, 7 routes prerendered
```

## Reviewer pass

- Frontend Reviewer agent: re-review after this commit lands. The
  page-level `'use client'` is the most important fix; everything
  else is style/clarity.
- The component is reusable as-is for wave 5. No breaking changes
  expected.
