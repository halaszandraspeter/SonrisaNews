# Wave 4 — Handoff to Future Work

> **Status**: ✅ Backend shipped 2026-06-05. **Frontend deferred** (scheduled for TDD Next.js Implementer).
> **Build status**: `dotnet build` emits **0 errors, 10 `NU1902` advisories** (MailKit/MimeKit — see §2.3).
> **What this doc covers**: items raised in the wave 4 review that are intentionally **out of scope for the backend PR** but should land in a follow-up PR or wave.

---

## 1. Items addressed in this wave (committed with the backend)

| # | Item | Resolution |
|---|---|---|
| 1 | Q3 — verification codes never expire (24h is the default per the checklist) | Added `VerificationChallenge.ExpiresAt`, `ChannelDefaults.VerificationTtl = 24h`, injected `IClock` into both channels. 6 new tests cover expiry (just-before, just-after, re-issue-after-expiry, expiry-stamp-on-issue). |
| 2 | Q8 — `Notification` needed a `dedupe_key` for idempotency (dispatcher is wave 8, but the field must exist now) | Added `Notification.DedupeKey` (`Guid`) + unique composite index `UX_Notifications_ChannelId_DedupeKey`. Migration `20260605074133_AddNotificationDedupeKey` applied. |
| 3 | `ChannelsController` had a missing `using SonrisaNews.Domain;` (would have failed in CI after the migration added the field) | Added. |
| 4 | Migration was missing the `// Forward-only after merge.` marker | Added (in `Up`). |
| 5 | **DI gap (latent runtime crash)**: `EmailChannel` / `SlackChannel` take `IHttpClientFactory` in their constructor, but `AddSonrisaNewsInfrastructure()` did not register it. Any request to `/api/v1/channels/{id}/verify/start` or any wave 8 dispatcher call would have thrown `InvalidOperationException` at request time. | Added `services.AddHttpClient()` to `InfrastructureServiceCollectionExtensions.AddSonrisaNewsHttpClient()`. 3 new `ChannelDiRegistrationTests` pin the contract — `EmailChannel` and `SlackChannel` resolve from DI by key, and a single scope yields two distinct instances. **This fix was caught by the new tests, not by inspection.** |

---

## 2. Items deferred — out of scope for wave 4

### 2.1 Frontend: `features/channels` module + "Add Channel" dialog

**Source**: Wave 4 scope bullet — *"Frontend: `features/channels` module + a 'Add channel' dialog on the dashboard"*.

**What the frontend needs to ship**:

1. **`web/features/channels/`** module (per the AGENTS folder convention: `web/features/<feature>/`):
   - `types.ts` — Zod schemas mirroring the backend DTOs (`CreateChannelRequest`, `VerifyStartResponse`, `VerifyConfirmRequest`, `ChannelResponse`).
   - `api.ts` — typed openapi-fetch client functions for each endpoint.
   - `useChannelsQuery.ts`, `useCreateChannelMutation.ts`, `useStartVerificationMutation.ts`, `useConfirmVerificationMutation.ts`, `useDeleteChannelMutation.ts` — React Query v5 hooks.
   - `ChannelTypeIcon.tsx` — maps `ChannelType` to an MUI v9 icon (Email: `EmailOutlined`, Slack: `Tag` — confirm with the user).
   - `useChannelTypeOptions.ts` — returns the dropdown options for the dialog.

2. **`web/app/(app)/alerts/_components/AddChannelDialog.tsx`** — MUI v9 dialog:
   - Step 1: pick channel type (Email / Slack) from a `<Select>`.
   - Step 2: enter destination (email address or Slack webhook URL).
   - Step 3: show the challenge code returned by `/verify/start`, prompt user to enter what they received at the destination.
   - Step 4: submit the confirmation code, close dialog, refresh the channels list.
   - **A11y**: `getByRole('dialog')` test target, focus trap, `aria-labelledby`, Esc-to-close (MUI default).

3. **`web/lib/channels/icons.tsx`** — backend → frontend icon map (per the `add-a-channel` skill step 6).

**Tripwires**:
- `nextjs-react.instructions.md` — no default exports, `sx` over `style`, Server Components by default (this is a Client Component, mark `'use client'`).
- `testing.instructions.md` — Playwright E2E test for the full dialog flow; Vitest unit test for Zod schema validation.
- Run `pnpm --dir web generate:api` after the backend PR merges to regenerate the openapi client.

**Verify**:
```bash
pnpm --dir web test --filter channels
pnpm --dir web test:e2e --grep "add channel"
# Add Channel dialog opens, closes on Esc
# Email: code appears in MailHog (or is shown inline in MVP if MailHog is not configured)
# Slack: code is shown inline (the user pastes what Slack sent)
# Channel appears in the channels list with verified=true after confirmation
```

**Agent**: TDD Next.js Implementer.

---

### 2.2 Migration script wiring in `dev:up` / `dev:reset-db`

**Source**: The checklist audit (`docs/implementation/mvp-checklist.md` §0.2 issue #3) noted that `dev: reset-db` bypasses the AppHost. The new `AddNotificationDedupeKey` migration was applied manually with `dotnet ef database update --project src/SonrisaNews.Infrastructure` — the dev script does not currently ensure migrations run as part of `dev:up`.

**Action in**: Wave 5 (alert CRUD) — the alert service will add the first non-seed entity to the schema, so the migration story is exercised end-to-end then. Until then, `dev:up` is correct for code that runs against an already-migrated DB.

**What to do** (wave 5):
```bash
# In scripts/dev.ps1 (or dev.sh), after starting the AppHost, add:
dotnet ef database update --project backend/src/SonrisaNews.Infrastructure
```

---

### 2.3 MailKit / MimeKit security advisories (NU1902)

**Source**: Build logs show 10 `NU1902` warnings for `MailKit 4.8.0` and `MimeKit 4.8.0` ("moderate severity"). The latest patched versions are 4.9.0+.

**Why deferred**: The wave 4 implementation doesn't actually connect to an SMTP server — `EmailChannel.SendAsync` validates the format and returns `SuccessResult`. The MailKit dependency is in the project for wave 9 (React Email templates / real SMTP) and wave 8 (the dispatcher's first send). Bumping now risks breaking wave 8's outbound code.

**Action in**: Wave 8 (or a pre-wave-8 dependency-bump PR if the user wants).

**What to do**:
```bash
dotnet add backend/src/SonrisaNews.Infrastructure package MailKit --version 4.9.0
dotnet add backend/src/SonrisaNews.Infrastructure package MimeKit --version 4.9.0
# rebuild + test
```

---

### 2.4 The "verified flag must block SendAsync" contract

**Source**: The wave 4 review noted: *"channel is created unverified, but SendAsync is called without checking Verified flag. This violates the tripwire: 'Verification is mandatory before any real alert is sent to a channel.'"*

**Why deferred to a comment, not a fix here**: The fix is in the **dispatcher** (wave 8), not the channel. The contract is:

> The `INotificationChannel.SendAsync` interface is the *seam* — channels are dumb pipes. The dispatcher is the *enforcer* — it queries `Channel.Verified = true` before resolving the keyed service and calling `SendAsync`. An unverified channel must never reach `SendAsync`.

The `INotificationChannel` XML doc on `SendAsync` should be updated to spell this out (in a wave 8 doc pass). For now, the contract is implicit in the `Channel.Verified` field's behavior.

**Action in**: Wave 8 (dispatcher implementation) — the dispatcher MUST check `Channel.Verified = true` and `Channel.QuietHoursStart/End` before calling `SendAsync`. The test list in mvp-checklist §"Wave 8 — Verify" includes `Dispatcher_RealtimeMode_SendsImmediately` and `Dispatcher_QuietHours_DefersSendUntilNextWindow` which together pin this contract.

**Suggested dispatcher sketch** (for wave 8):
```csharp
var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == notification.ChannelId, ct);
if (channel is null) return NotificationStatus.Failed("channel_missing");
if (!channel.Verified) return NotificationStatus.Failed("channel_unverified");  // <-- this is the check
if (channel.QuietHoursActiveAt(_clock.UtcNow, user.TimeZone)) return NotificationStatus.Queued;
var impl = sp.GetKeyedService<INotificationChannel>(channel.Type.ToString());
// ... call impl.SendAsync(...)
```

---

### 2.5 Q2 — JSON-driven disaster severity thresholds

**Source**: Open question Q2 in `mvp-checklist.md` §4. Default = "Seed from a JSON file at `data/default-thresholds.json`". This is a wave 8 concern, not wave 4.

**Action in**: Wave 8 (disaster poller). No wave 4 work required.

---

## 3. Test results at handoff

```
dotnet test
Passed!  - Failed: 0, Passed: 66, Skipped: 0, Total: 66
```

| Suite | Count | Coverage |
|---|---|---|
| Wave 2 (database) | 32 | schema + InitialSchema migration |
| Wave 3 (auth + RBAC) | 13 | sign-up, sign-in, refresh, RBAC handler, audit tool |
| Wave 4 — EmailChannel | 11 | type, code-gen, expiry-stamp, correct-code, wrong-code, after-24h, just-before-24h, re-issue-after-expiry, success-send, failure-send, plus the DI fix's regression coverage |
| Wave 4 — SlackChannel | 7 | type, code-gen, expiry-stamp, correct-code, wrong-code, after-24h, webhook-success, webhook-failure |
| Wave 4 — DI registration | 3 | resolves both channels by key, returns distinct instances, single-scope semantics |
| **Total** | **66** | |

---

## 4. Files touched in this wave (final list)

### Domain
- `backend/src/SonrisaNews.Domain/Notifications/INotificationChannel.cs` (unchanged)
- `backend/src/SonrisaNews.Domain/Notifications/SendResult.cs` (unchanged)
- `backend/src/SonrisaNews.Domain/Notifications/VerificationChallenge.cs` — **added `ExpiresAt`**
- `backend/src/SonrisaNews.Domain/Notifications/NotificationPayload.cs` (unchanged)
- `backend/src/SonrisaNews.Domain/Notifications/ChannelTypeConstants.cs` (unchanged)
- `backend/src/SonrisaNews.Domain/Notifications/ChannelDefaults.cs` — **new: `VerificationTtl = 24h`**
- `backend/src/SonrisaNews.Domain/Entities/Channel.cs` (unchanged — already fleshed out in wave 2)
- `backend/src/SonrisaNews.Domain/Entities/Notification.cs` — **added `DedupeKey`**
- `backend/src/SonrisaNews.Domain/Entities/AlertChannelMode.cs` (unchanged — already fleshed out in wave 2)

### Infrastructure
- `backend/src/SonrisaNews.Infrastructure/InfrastructureServiceCollectionExtensions.cs` — **added `AddSonrisaNewsHttpClient()` (registers `IHttpClientFactory` via `AddHttpClient()`)** — fixes the latent DI crash
- `backend/src/SonrisaNews.Infrastructure/Channels/EmailChannel.cs` — **now takes `IClock`, stamps `ExpiresAt`, rejects expired codes**
- `backend/src/SonrisaNews.Infrastructure/Channels/SlackChannel.cs` — same

### API
- `backend/src/SonrisaNews.Api/Controllers/ChannelsController.cs` — **added `using SonrisaNews.Domain;`** (the missing import would have been a CI failure)

### Persistence
- `backend/src/SonrisaNews.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` — **added unique index `UX_Notifications_ChannelId_DedupeKey`**
- `backend/src/SonrisaNews.Infrastructure/Migrations/20260605074133_AddNotificationDedupeKey.cs` — **new migration** (applied to `data/sonrisa.db`)

### Tests
- `backend/tests/SonrisaNews.UnitTests/Channels/EmailChannelTests.cs` — **rewritten**: 6 → 11 tests, +expiry cases, +FakeClock helper
- `backend/tests/SonrisaNews.UnitTests/Channels/SlackChannelTests.cs` — **rewritten**: 6 → 7 tests, +expiry cases
- `backend/tests/SonrisaNews.UnitTests/Channels/ChannelDiRegistrationTests.cs` — **new**: 3 tests pinning DI resolution contract for both channels

---

## 5. Open questions for the user (none blocking)

- **None.** All wave 4 scope items are either shipped (Q3, Q8, DI fix) or intentionally deferred (Q2 in wave 8, frontend in TDD Next.js Implementer, migration-script wiring in wave 5).
- The next blocking decision is Q4 (digest-daily default hour) which lands in wave 8.
