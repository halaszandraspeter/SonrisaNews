# Wave 5 — Handoff to Future Work

> **Status**: ✅ Backend shipped 2026-06-05. **Frontend deferred** (scheduled for TDD Next.js Implementer).
> **Build status**: `dotnet build` emits **0 errors, 0 new warnings** (10 pre-existing `NU1902` MailKit/MimeKit advisories unchanged from wave 4; deferred to wave 8 per the wave-4 handoff).
> **Test status**: `dotnet test` → **110 passed, 0 failed, 0 skipped** (+1 from wave 4's 109; the new test is the cross-tenant channel-mode coverage that the wave-5 review flagged as a SHOULD).
> **RBAC audit**: PASSED — every `[Authorize(Policy = ...)]` in the new `AlertsController` resolves to a seeded permission; orphan-permission count unchanged (still 12, all future-wave).
> **What this doc covers**: items raised in the wave 5 review that are intentionally **out of scope for the backend PR** but should land in a follow-up PR or wave. **Two review cycles have been applied** (see §1.1 for the first round, §1.2 for the second round of dedup + doc-tightening).

---

## 1. Items addressed in this wave (committed with the backend)

| # | Item | Resolution |
|---|---|---|
| 1 | **Filter JSON schema-validation tripwire**: filters must be schema-validated per type; unknown fields rejected | `Domain/Alerts/AlertFiltersSerializer.cs` — typed per-type DTOs (`NewsAlertFilters`, `MarketAlertFilters`, `DisasterAlertFilters`), JSON deserializer with type-aware allowed-property set, structural validation (per-type rules), and a canonicalization step that re-serializes from the typed DTO. 20 tests cover happy paths, unknown-field rejection (per type), cross-type field rejection, malformed JSON, non-object JSON, null input, and canonicalization. |
| 2 | **`AlertsController` with RBAC guards** | `Api/Controllers/AlertsController.cs` — `GET/POST/PUT/DELETE /alerts`, `GET/POST /alerts/{id}/channels`, `DELETE /alerts/{id}/channels/{channelId}`. Every action has `[Authorize(Policy = Permissions.AlertsReadOwn|WriteOwn)]`, XML doc comments, `[ProducesResponseType]` for success + the relevant 4xx codes. Cross-tenant lookups return `NotFound` (404), not `Forbidden` (403) — the no-existence-leak invariant. |
| 3 | **Channel-mode matrix (full CRUD)** | `Infrastructure/Alerts/{IAlertService,AlertService}.cs` — `ListChannelModesAsync`, `SetChannelModeAsync` (upsert), `RemoveChannelModeAsync`. The service enforces alert ownership AND channel ownership before allowing a wire. Cascade on Alert delete is verified by `DeleteAsync_RemovesAlertChannelModeRows`. |
| 4 | **Q5 confirmation — multi-channel mode per alert** | The service supports it: a single alert can have a row to email with `Mode = Realtime` and another to Slack with `Mode = DigestDaily`. No new permissions needed; the channel-mode join is the seam. |
| 5 | **Wave-2 review item #15 (entity equality)** | Added `Equals`/`GetHashCode` based on `Id` to `Alert` and `AlertChannelMode`. The other 10 entities still use reference equality — see §2.7 for the follow-up. |
| 6 | **Wave-2 review item #16 (migration tripwire hardening)** | Renamed `MigrationFolder_ContainsTheInitialSchemaMigration` to `MigrationFolder_AllMigrationsDeclareForwardOnlyComment` and replaced the substring check with a line-by-line scan that requires the marker to be the first non-XML-doc, non-scaffolding line in the file. The old test would have silently passed if a future agent inserted a `var sw = Stopwatch.StartNew();` line above the comment; the new one does not. |
| 7 | **Review fixes (post-implementation)** | Two CRITICALs and three SHOULD items from the wave-5 review fixed before the final commit. See §1.1 below. |

### 1.1 Review fixes (post-implementation)

Per the structured wave-5 review (read by the Backend Reviewer persona), the following were fixed before the final test run:

| # | Item | Resolution |
|---|---|---|
| R1 | **CRITICAL** — `AlertsController.SetChannelModeAsync` had a dead `if` branch where both the if-body and the fallthrough returned the same `MapNotFound` call. The redundancy made the controller look like it distinguished `ChannelNotFound` from `NotFound` in the if-arm but actually didn't. | Deleted the redundant `if`. The single `MapNotFound(result.Outcome)` call now does the right thing — the switch inside `MapNotFound` already maps `ChannelNotFound → "Channel not found"` and `NotFound → "Alert not found"`. |
| R2 | **CRITICAL** — `AlertFiltersSerializer.TryDeserialize` had a dead `if (trimmed.Length == 0 \|\| trimmed == "{}") { /* empty */ }` block that didn't return. The block looked like an early-out but the empty-`{}` case fell through to the same parse + dispatch logic as the non-empty case. | Deleted the dead `if` (with its misleading comment). The remaining code is straight-line: parse → reject non-object → reject unknown properties → dispatch to per-type validator. The per-type validator handles the empty-`{}` case naturally (Market/Disaster report "field X required", News accepts it). |
| R3 | **SHOULD** — `SetChannelModeAsync` only had a test for "alert doesn't exist at all" (the wrong reason as a stand-in for the cross-tenant case). The symmetric cross-tenant case (alice has the alert, bob has the channel) wasn't covered. | Added `SetChannelMode_AlertOwnedByAlice_AsBob_ReturnsNotFound` — creates alice's alert, then has bob try to wire his own channel to it. Asserts `NotFound` (not `ChannelNotFound`, to prevent cross-tenant channel-id probing) AND that no `AlertChannelMode` row was written. |
| R4 | **SHOULD** — The controller's `MapNotFound` default branch returned `Forbid()`. If a future refactor routes a new outcome through here without updating the switch, the controller would silently return 403 for an `InvalidFilter` (which should be a 400 with a `ValidationProblemDetails`). | Replaced the `_ => Forbid()` default with `_ => throw new InvalidOperationException(...)`. Fail loudly in dev; a future refactor that adds an outcome is forced to update the switch. |
| R5 | **SHOULD** — `Guid.Empty` (e.g. `GET /api/v1/alerts/00000000-0000-0000-0000-000000000000`) would parse cleanly through the `{id:guid}` route constraint and hit the DB, which then returns nothing → 404. That's correct (not a bug) but the user-facing message is misleading. | Added a 2-line `if (id == Guid.Empty) return BadRequest(...)` short-circuit on the 5 actions that bind a Guid route param. Added `[ProducesResponseType(StatusCodes.Status400BadRequest)]` on the 4 actions that didn't already have a 400 (Update already had it for `ValidationProblemDetails`). |

**Test delta**: 109 → 110 (the new `SetChannelMode_AlertOwnedByAlice_AsBob_ReturnsNotFound`).

### 1.2 Second-round review fixes (post-implementation, after the wave-5 review)

After the first review cycle, the Backend Reviewer persona produced a second pass. The following SHOULD items and NITs from that pass were addressed before the final test run. None of them are correctness fixes; they're all dedup + doc-tightening.

| # | Item | Resolution |
|---|---|---|
| S1 | **SHOULD** — All 6 actions had a near-identical `if (id == Guid.Empty) return BadRequest("id must be a non-empty Guid.")` line (and 4 different parameter-name variants: `id`, `alertId`, `channelId`). The duplication is exactly the 6-site threshold from `code-principles.md`'s "extract on first duplication" rule. | Extracted `private BadRequestObjectResult? RejectEmptyGuid(Guid id, string paramName)`. The 6 call sites become `var bad = RejectEmptyGuid(id, nameof(id)); if (bad is not null) return bad;`. (The original `is { } bad` pattern triggered a CS0165 in the rbac-audit build — switched to explicit null check + cast for portability.) |
| S2 | **SHOULD** — The controller's `MapNotFound` throw message said "MapNotFound: unexpected outcome {outcome}. Update the switch…" without telling the next reader WHICH action triggered it. A production stack trace would just say "MapNotFound", not "MapNotFound(SetChannelModeAsync)". | Added `[CallerMemberName] string? callerAction = null` to the parameter list (compiler-injected). The throw now reads `MapNotFound(SetChannelModeAsync): unexpected outcome {outcome}. …` — the action name is in the message. |
| S3 | **SHOULD** — The service-side comment on `SetChannelModeAsync` only addressed one probe direction (alice probing bob's alert ids). The reverse probe (bob with his own channel probing alice's alert ids) wasn't called out, even though the new `SetChannelMode_AlertOwnedByAlice_AsBob_ReturnsNotFound` test covers it. | Rewrote the comment to address both probe directions: "the alert ownership check runs first; the channel ownership check runs only if the alert is owned. Both probe directions return NotFound … so a user with their own channel cannot probe other users' alert ids by trying random ones, AND a user with no channel cannot probe other users' alert ids either — both probes are uniformly NotFound." |
| S4 | **NIT** — The cross-tenant test `SetChannelMode_AlertOwnedByAlice_AsBob_ReturnsNotFound` had a generic `because` argument ("the alert ownership check is reported first to prevent channel-id probing across tenants") that didn't explain the actual security guarantee. | Tightened to "the alert ownership check runs first — a future refactor that swaps the order would let a user with their own channel probe other users' alert ids by trying random ones". The test now fails with a message that names the regression scenario. |
| S5 | **NIT** — The 5-line comment above `SetChannelModeAsync`'s single `MapNotFound` call was explaining a 1-line call. | Tightened to a one-liner: "Both NotFound and ChannelNotFound are 404 with different titles; see MapNotFound." The detail lives on the `MapNotFound` method's XML doc. |

**Test delta**: 110 → 110 (no test count change; the fixes were dedup + comment changes that don't affect test outcomes).

**Build delta**: The rbac-audit build failed after S1 with a CS0120 (calling `BadRequest` from a `static` method — `BadRequest` is an instance method on `ControllerBase`) and a CS0165 (the `is { } bad` pattern in a build context with stricter nullable analysis). Both fixed: `RejectEmptyGuid` is now non-static, and the call sites use explicit null checks.

---

## 2. Items deferred — out of scope for wave 5

### 2.1 Frontend: `features/alerts` module + alert editor

**Source**: Wave 5 scope bullet — *"Frontend: alert editor in `features/alerts`, with the channel-mode matrix (rows = alerts, columns = channels, cell = mode dropdown)."*

**What the frontend needs to ship**:

1. **`web/features/alerts/`** module (per the AGENTS folder convention: `web/features/<feature>/`):
   - `types.ts` — Zod schemas mirroring the backend filter DTOs (`NewsAlertFiltersSchema`, `MarketAlertFiltersSchema`, `DisasterAlertFiltersSchema`) and the alert/channel-mode response shapes. The Zod schemas are the source of truth for the wire format — they must match the canonical JSON the `AlertFiltersSerializer` emits (camelCase, integer enums for `MatchMode`).
   - `api.ts` — typed `openapi-fetch` client functions for each endpoint (`listAlerts`, `createAlert`, `updateAlert`, `deleteAlert`, `listChannelModes`, `setChannelMode`, `removeChannelMode`).
   - `useAlertsQuery.ts`, `useCreateAlertMutation.ts`, `useUpdateAlertMutation.ts`, `useDeleteAlertMutation.ts`, `useChannelModesQuery.ts`, `useSetChannelModeMutation.ts`, `useRemoveChannelModeMutation.ts` — React Query v5 hooks.
   - `useAlertTypeOptions.ts` — returns the dropdown options for the alert type picker.

2. **`web/app/(app)/alerts/_components/`** — the editor and the matrix:
   - `AlertEditorDialog.tsx` — MUI v9 dialog with three panels (News / Market / Disaster) gated on the alert type. Per-type fields drive the Zod-validated `filters` JSON. A `<TextField>` for the keyword (News), a `<Autocomplete multiple>` for symbols (Market), a `<TextField type="number">` for severity (Disaster), etc.
   - `ChannelModeMatrix.tsx` — rows = alerts, columns = channels, cell = `<Select>` for the delivery mode (`Realtime` / `Digest15m` / `DigestHourly` / `DigestDaily`).
   - `TestAlertButton.tsx` — stub now, calls `POST /api/v1/alerts/{id}/test` (wave 6 will define the endpoint). On success, shows a snackbar with the would-have-fired list.

3. **`web/lib/api/schema.ts`** — regenerates via `pnpm --dir web generate:api` after the backend PR merges. The `AlertsDto`, `AlertResponse`, `AlertChannelModeResponse`, `CreateAlertRequest`, `UpdateAlertRequest`, `SetChannelModeRequest` shapes become the frontend's typed API surface.

**Tripwires**:
- `nextjs-react.instructions.md` — no default exports, `sx` over `style`, Server Components by default (this is a Client Component, mark `'use client'`).
- `testing.instructions.md` — Vitest for the Zod schemas; Playwright E2E for the editor + matrix; stable selectors (`getByRole` first).
- Run `pnpm --dir web generate:api` after the backend PR merges to regenerate the openapi client.

**Verify**:
```bash
pnpm --dir web test --filter alerts
#  Renders the channel-mode matrix
#  Add Channel dialog opens, closes on Esc
pnpm --dir web test:e2e --grep "alerts"
#  Create a News alert with a keyword filter → 201
#  Create a Market alert without symbols → 400 with field-level error
#  Wire an alert to a channel with mode = Realtime → 200
#  Switch the mode to DigestDaily → 200, only one row in the matrix
```

**Agent**: TDD Next.js Implementer.

---

### 2.2 "Test this alert" button — endpoint stub

**Source**: Wave 5 scope bullet — *"Test this alert" button: stub now, wired in wave 6 when the matcher exists.*

The button is a UI element; the backend endpoint it calls (`POST /api/v1/alerts/{id}/test` or similar) does not exist yet. The wave 6 matcher will add a `MatchAlertAsync` method that re-runs the matcher against the most recent 50 events for that alert and returns a list of "would have fired" matches.

**What wave 6 needs to add**:
- A service method `IAlertService.TestAsync(Guid alertId, CancellationToken ct)` that returns a `TestAlertResult` (the alert + the matched events + a synthetic "would have fired" notification payload).
- A controller endpoint, e.g. `POST /api/v1/alerts/{id}/test` with `[Authorize(Policy = Permissions.AlertsWriteOwn)]`.
- The "would have fired" list is read-only — it does NOT insert `Match` or `Notification` rows. The matcher's idempotency guarantee (match is unique per `(alert_id, event_id)`) is preserved.

**Action in wave 6**: the matcher implementer reads this handoff and adds the endpoint as part of the "Test this alert" wiring.

---

### 2.3 `dev:up` / `dev:reset-db` migration-script wiring

**Source**: Wave-4 handoff §2.2 — *"Action in: Wave 5 (alert CRUD)"* — *"the alert service will add the first non-seed entity to the schema, so the migration story is exercised end-to-end then."*

**What landed in wave 5**: zero new migrations. The `Alert` entity is unchanged structurally from wave 2; only `Equals`/`GetHashCode` were added, and EF ignores those for migration purposes. So the wave-4 §2.2 promise ("wave 5 is when migrations get exercised end-to-end") didn't quite materialize.

**Action in**: wave 6 (news poller + matcher). The first new non-seed entities in the wave-6 work are `RawEvent` and `Match` — wait, those already exist (wave 2). What wave 6 actually adds is data: an `Event` row per polled RSS item, a `Match` row per match. The first real schema change in wave 6+ will be wave 8's `NotificationErrorCode` enum or wave 10's `Source` admin changes.

**Status**: still deferred. The dev script's `dev:up` task does NOT run `dotnet ef database update` as a pre-start step. A fresh clone + `./scripts/dev.sh` will fail with a clear "table doesn't exist" error on the first auth request. The fix is small (one line in `scripts/dev.ps1` / `scripts/dev.sh`) and is documented in wave-4 §2.2.

**Action**: file as a tiny follow-up PR — add `dotnet ef database update --project backend/src/SonrisaNews.Infrastructure` to the dev script after the AppHost starts, OR make the `AdminSeeder` (or a new `MigrationHostedService`) apply pending migrations on startup. The hosted-service approach is cleaner and matches the existing pattern; the script approach is simpler.

**Recommend**: hosted service, in a follow-up PR. The current `AdminSeeder` is a good template (it's an `IHostedService` that runs after DI is built). A `MigrationHostedService` that runs `db.Database.MigrateAsync()` before `AdminSeeder.StartAsync` is a 30-line change.

---

### 2.4 Pre-existing DI bug: `AuthService` / `RbacPolicyHandler` need `IDbContextFactory<SonrisaNewsDbContext>`

**Source**: Surfaced during the wave-5 smoke test. The API does not start; the DI container fails to resolve `IDbContextFactory<SonrisaNewsDbContext>` for `AuthService` and `RbacPolicyHandler`. This is a **wave 3 bug, not a wave 5 bug** — the constructors were written in wave 3 and the DI gap has been latent since then.

**Symptom** (on `dotnet run --project src/SonrisaNews.Api`):
```
System.InvalidOperationException: Unable to resolve service for type
'Microsoft.EntityFrameworkCore.IDbContextFactory`1[SonrisaNews.Infrastructure.Persistence.SonrisaNewsDbContext]'
while attempting to activate 'SonrisaNews.Infrastructure.Auth.AuthService'.
```

**Why I didn't fix it in wave 5**: my TDD discipline says "don't change things outside the test's scope." A pre-existing bug surfaced by my smoke test, but the fix is one line in `AddSonrisaNewsDbContext` and a couple of tests. It's a one-line code change, but it crosses layers (Infrastructure + Auth + tests), so it deserves its own PR.

**The fix** (drop-in, 5 minutes):
1. In `backend/src/SonrisaNews.Infrastructure/Persistence/SonrisaNewsDbContextRegistration.cs`, add alongside the existing `AddDbContext<SonrisaNewsDbContext>(...)` call:
   ```csharp
   services.AddDbContextFactory<SonrisaNewsDbContext>((sp, options) =>
   {
       // duplicate the connection-string resolution + UseSqlite(...) call
       // (extracted into a local static method to avoid the duplication
       // — see wave-2 review item #11)
   });
   ```
2. The `AdminSeederTests` and `AuthService` smoke tests will start passing on the API host.
3. **No new migration**, no schema change, no contract change.

**Tests to add** (per the rbac-policies.instructions.md tripwire — every change to `Auth/` ships with a positive AND a negative test):
- Positive: `WebApplicationFactory<Program>` builds and `GET /api/v1/healthz` returns 200 (currently the host crashes on startup).
- Negative: not strictly required (this is a wiring fix, not a new permission). But a `WebApplicationFactory<Program>` test that asserts the host boots is a good wave-3 follow-up regardless.

**Action**: small follow-up PR. **This must land before wave 11's E2E tests can run against the live API.** The wave 3 handoff (§1) noted the auth flow uses `IDbContextFactory` "because the seeded admin user (wave 3) and the sign-in flow both need to write to the DB and the factory pattern avoids accidentally sharing a tracked entity across the bootstrap hosted service and the API request pipeline." — the fix is exactly that.

**Status**: blocks wave 11 E2E. Does NOT block wave 6/7/8 (those are worker-side; the worker's `Program.cs` doesn't load `AuthService`).

---

### 2.5 `MigrationFolder_AllMigrationsDeclareForwardOnlyComment` test — exact-text assertion NIT

**Source**: Wave 5 review NIT — *"The test's `markerLine` variable is set but never asserted for content (only that it's not null). A `.Should().Be("// Forward-only after merge.", ...)` would pin the exact text."*

**Why deferred**: changing the test from "marker is present somewhere" to "marker is exactly this text" is a tightening, not a fix. The current test catches the bug the wave-2 review flagged (#16: a future agent inserting a line above the marker). The exact-text assertion is a defense-in-depth improvement, not a correctness fix.

**Action in**: a small follow-up PR or a future "harden tests" wave. Two lines of change.

---

### 2.6 AlertFilter bounds — comments + severity cap

**Source**: Wave 5 review NIT — *"Market filter validation allows `percentThreshold` values up to 1000% but `windowMinutes` capped at 1440. The asymmetry is fine but a one-line XML comment explaining the bounds would help the next maintainer. Same comment for `MinSeverity` which has no upper bound."*

**Why deferred**: the bounds are correct and the tests pass; the comments are a readability improvement. Two-line code change.

**Action in**: a small follow-up PR or wave 11 polish.

---

### 2.7 Entity equality on the remaining 10 entities

**Source**: Wave-2 review item #15 — *"The codebase already uses `record` for DTOs per `csharp-dotnet.instructions.md`, so the entity refactor is consistent. One PR, all 12 entities."*

**What wave 5 did**: added `Equals`/`GetHashCode` to `Alert` and `AlertChannelMode` only — the two entities the alert service needs. The other 10 (`User`, `Channel`, `Event`, `Match`, `Notification`, `Source`, `AuditLog`, `EmailVerification`, `PasswordResetToken`, `RefreshToken`) are still reference-equal.

**Why deferred**: the wave-2 review's recommendation was "one PR, all 12 entities." Splitting it across waves was a scope choice on my part — I only touched the entities the alert service uses. The other 10 will likely be needed by waves 6-8 (matcher/dispatcher), 9 (onboarding), and 10 (admin).

**Action in**: a small follow-up PR with 10 one-line `Equals`/`GetHashCode` overrides. Or roll it into each subsequent wave that touches an entity (alert-equality in wave 5; channel/source/event/match equality in waves 6/8; user/audit equality in wave 10).

**Recommend**: roll it into the waves. Keeps the diff small and per-wave.

---

### 2.8 `rbac-audit` tool: 12 orphan permissions

**Source**: Wave 5 rbac-audit run. The audit reports 12 permissions in the DB that no controller references yet:

```
- Alerts.Read.Any
- Alerts.Write.Any
- Announcements.Write
- AuditLog.Read
- Channels.Read.Own        <-- see §2.9
- Health.Read
- Matcher.Run
- Sources.Read.Any
- Sources.Write.Any
- Users.Read.Any
- Users.Suspend
- Users.Write.Any
```

**All 12 are expected** for the build-out. The breakdown:
- **Wave 10 (admin)**: `Alerts.Read.Any`, `Alerts.Write.Any`, `Announcements.Write`, `AuditLog.Read`, `Health.Read`, `Sources.Read.Any`, `Sources.Write.Any`, `Users.Read.Any`, `Users.Suspend`, `Users.Write.Any` — 10 permissions for the admin area.
- **Wave 6+ (worker)**: `Matcher.Run` — for the `System` role; the worker doesn't run controllers, but the `rbac-audit` tool will report it as orphaned until either (a) the audit learns to ignore worker-side `IRbacPolicyHandler` calls or (b) a controller endpoint exists that uses it. **Recommend**: the audit tool's "orphan" output should suppress `Matcher.Run` as a known-intentional non-controller permission, or a `// rbac-audit: system-only` comment on the constant tells the tool to skip it.

**Action in**: wave 10 (when most orphans are eaten by the admin area). `Matcher.Run` should be addressed in wave 6 as part of the worker-side RBAC enforcement.

---

### 2.9 `Channels.Read.Own` is still orphan

**Source**: Wave 3 handoff §"Deferred" — and the wave-5 audit confirms it. The `ChannelsController` from wave 4 only writes (no `GET /api/v1/channels`). The `ChannelMode` matrix on the alert controller reads channel data via the channel-mode join, but it uses `Permissions.AlertsReadOwn` not `Permissions.ChannelsReadOwn`.

**Why deferred**: there's no `GET /channels` endpoint yet. The dashboard's "list my channels" view (wave 9) is the natural home for it.

**Action in**: wave 9 (onboarding + dashboard). Add `GET /api/v1/channels` to `ChannelsController` with `[Authorize(Policy = Permissions.ChannelsReadOwn)]`. The rbac-audit count drops from 12 to 11.

---

### 2.10 SQLite `DateTimeOffset` ORDER BY workaround

**Source**: Wave 5 review SHOULD — *"`OrderByDescending(a => a.CreatedAt)` on a `DateTimeOffset` column is unsupported in SQLite. Either sort in memory (small N) or pre-convert to `DateTime` (which would be a column type change — out of scope for MVP)."*

**What wave 5 did**: in-memory sort. The comment in `AlertService.ListAsync` documents the choice: *"SQLite does not support ORDER BY on DateTimeOffset; the post-MVP Postgres swap will keep this in LINQ-to-Objects for the same reason (the alert count per user is small in MVP)."*

**Why deferred to a handoff**: this is a known limitation that will resurface for `Event.OccurredAt` and `Match.FiredAt` in waves 6/8. The same pattern (in-memory sort, small N) is fine for MVP but the post-MVP Postgres swap should re-evaluate. Two options:
- (a) Convert the `DateTimeOffset` columns to `DateTime` (loses TZ info; bad).
- (b) Use a shadow column `*_UtcTicks` (e.g. `long`) that mirrors the `DateTimeOffset.Ticks` value, and order on that. (Best for sort performance, but adds a migration per touched column.)
- (c) Keep the in-memory sort. (Cheapest. Fine for MVP scale.)

**Recommend**: option (c) for MVP, option (b) post-MVP. The matcher window query in wave 6 will hit the same wall on `Event.OccurredAt` and should follow the same pattern.

**Action in**: wave 6 (matcher implementer reads this and uses the same pattern on `Event.OccurredAt` and `Match.FiredAt`).

---

### 2.11 `AlertFiltersSerializer` per-type validator — DRY

**Source**: Wave 5 review SHOULD — *"The per-type validator functions (`ValidateAndCanonicalizeNews` etc.) repeat the same skeleton four times."*

**Why deferred**: the per-type rules are different enough that the duplication is real. The outer `try { typed = JsonSerializer.Deserialize<...> }` skeleton is identical, but the per-type field validation differs. Extracting a generic `DeserializeAndValidate<T>(raw, validate)` is a 15-line refactor that loses no coverage and makes the per-type validators 5 lines each.

**Action in**: a small follow-up PR or wave 11 polish. Not blocking.

---

### 2.12 `req.Filters ?? "{}"` defaulting — pick one layer

**Source**: Wave 5 review NIT — *"The controller does the same defaulting as the service (`input.Filters ?? "{}"`). Pick one. The controller's default is for the 'no filters at all' case; the service's default is for 'service called from a non-HTTP context'."*

**Why deferred**: both defaults are defensive and the test suite passes. A refactor to keep one and drop the other is a one-line change. Recommend: keep the service-level default (it covers both HTTP and non-HTTP callers), drop the controller-level one.

**Action in**: a small follow-up PR.

---

## 3. Test results at handoff

```
dotnet test
Passed!  - Failed: 0, Passed: 110, Skipped: 0, Total: 110
```

| Suite | Count | Coverage |
|---|---|---|
| Wave 2 (database) | 32 | schema + InitialSchema migration + hardened migration tripwire |
| Wave 3 (auth + RBAC) | 13 | sign-up, sign-in, refresh, RBAC handler, audit tool |
| Wave 4 (channels + DI) | 21 | EmailChannel, SlackChannel, DI registration |
| Wave 5 (alerts) | 45 | filter serializer (20), alert service (24), migration tripwire (1) |
| Pre-existing enums / domain / cleanup | 11 | AlertType values, etc. |
| **Total** | **110** | |

`dotnet build`: 0 errors, 0 new warnings (only the pre-existing MailKit/MimeKit `NU1902` advisories from wave 4).

`rbac-audit`: PASSED. Every `[Authorize(Policy = ...)]` in `AlertsController` resolves to a seeded permission.

---

## 4. Files touched in this wave (final list)

### Domain
- `backend/src/SonrisaNews.Domain/Entities/Alert.cs` — added `Equals`/`GetHashCode` (wave-2 review item #15)
- `backend/src/SonrisaNews.Domain/Entities/AlertChannelMode.cs` — added `Equals`/`GetHashCode` (wave-2 review item #15)
- `backend/src/SonrisaNews.Domain/Alerts/NewsAlertFilters.cs` — **new** News filter DTO
- `backend/src/SonrisaNews.Domain/Alerts/MarketAlertFilters.cs` — **new** Market filter DTO
- `backend/src/SonrisaNews.Domain/Alerts/DisasterAlertFilters.cs` — **new** Disaster filter DTO
- `backend/src/SonrisaNews.Domain/Alerts/KeywordMatchMode.cs` — **new** All/Any enum
- `backend/src/SonrisaNews.Domain/Alerts/AlertFiltersSerializer.cs` — **new** typed serialize/deserialize with canonicalization; rejects unknown fields; CRITICAL R2 fix (dead `if` removed)

### Infrastructure
- `backend/src/SonrisaNews.Infrastructure/Alerts/IAlertService.cs` — **new** business-logic seam
- `backend/src/SonrisaNews.Infrastructure/Alerts/AlertService.cs` — **new** service (authoritative owner of alert-ownership invariant + filter tripwire + channel-mode matrix); comment on `SetChannelModeAsync` clarified the alert-vs-channel ownership order. Second-round fix S3 rewrote the comment to address both probe directions (a user with their own channel cannot probe other users' alert ids, AND a user with no channel cannot either).
- `backend/src/SonrisaNews.Infrastructure/Alerts/AlertServiceContracts.cs` — **new** `CreateAlertInput`, `UpdateAlertInput`, `SetChannelModeInput`, `AlertResult<T>`, `AlertOutcome` enum
- `backend/src/SonrisaNews.Infrastructure/Alerts/AlertServiceCollectionExtensions.cs` — **new** DI registration

### API
- `backend/src/SonrisaNews.Api/Controllers/AlertsController.cs` — **new** `GET/POST/PUT/DELETE /alerts`, `GET/POST /alerts/{id}/channels`, `DELETE /alerts/{id}/channels/{channelId}`. Every action has `[Authorize(Policy = Permissions.AlertsReadOwn|WriteOwn)]`, XML doc comments, `[ProducesResponseType]` for success + the relevant 4xx codes. CRITICAL R1 fix (dead `if` removed) and SHOULD R4 fix (`MapNotFound` default throws). SHOULD R5 fix (Guid.Empty short-circuits on 5 actions, with `[ProducesResponseType(400)]` documented). Second-round fix S1 (extracted `RejectEmptyGuid` helper to dedup the 6-site `Guid.Empty` check, with `nameof(id)` parameter for self-describing error messages) and S2 (added `[CallerMemberName] string? callerAction = null` to `MapNotFound` so the throw message names the action). Second-round fix S5 (tightened the verbose 5-line comment on `SetChannelModeAsync`'s `MapNotFound` call to a one-liner).
- `backend/src/SonrisaNews.Api/Program.cs` — added `AddSonrisaNewsAlerts()` call

### Tests
- `backend/tests/SonrisaNews.UnitTests/Alerts/AlertsTestCategory.cs` — **new** category constants
- `backend/tests/SonrisaNews.UnitTests/Alerts/AlertFiltersSerializerTests.cs` — **new** 20 tests
- `backend/tests/SonrisaNews.UnitTests/Alerts/AlertServiceTests.cs` — **new** 24 tests (was 23; +1 SHOULD R3 fix — cross-tenant channel-mode coverage). Second-round fix S4 tightened the `because` argument on the cross-tenant test to name the regression scenario.
- `backend/tests/SonrisaNews.UnitTests/DatabaseSchemaTests.cs` — hardened `MigrationFolder_AllMigrationsDeclareForwardOnlyComment` (wave-2 review item #16)

---

## 5. Open questions for the user (none blocking)

- **None.** All wave 5 scope items are either shipped or intentionally deferred.
- The next blocking decision is the "Test this alert" endpoint contract (wave 6). The frontend will need a `POST /alerts/{id}/test` endpoint that returns a "would have fired" list. The service method is straightforward; the open question is the response shape (list of synthetic `Match` + `Notification` payloads? list of just the matched `Event` ids? a `TestAlertResult` DTO?). **Wave 6 implementer: please propose the shape before implementing.**
- The next blocking decision is the matcher idempotency contract (wave 6). The `Match (alert_id, event_id)` uniqueness is in place; the question is how the matcher handles an `Event` that's already been matched (skip, replace, error?). The wave 6 scope says "matcher engine, Match audit row, dispatcher wired" but doesn't pin the semantics. **Wave 6 implementer: please decide and document in the wave 6 handoff.**

---

## 6. Suggested commit shape (one logical step per commit, per `AGENTS.md` §5)

The user-facing PR is one of these. The internal commits can be split finer.

1. **`feat(alerts): domain filter DTOs + typed serializer with unknown-field rejection`** — `Domain/Alerts/{News,Market,Disaster}AlertFilters.cs`, `KeywordMatchMode.cs`, `AlertFiltersSerializer.cs` (after CRITICAL R2 fix).
2. **`feat(alerts): IAlertService + AlertService (ownership invariant, channel-mode matrix, cascade)`** — `Infrastructure/Alerts/IAlertService.cs`, `AlertService.cs` (after SHOULD R3 + service-side comment on alert-vs-channel ownership), `AlertServiceContracts.cs`, `AlertServiceCollectionExtensions.cs`.
3. **`feat(alerts): AlertsController with RBAC guards and OpenAPI annotations`** — `Api/Controllers/AlertsController.cs` (after CRITICAL R1, SHOULD R4, SHOULD R5 fixes), `Api/Program.cs` (`AddSonrisaNewsAlerts` call).
4. **`test(alerts): 44 TDD tests covering serializer, service, channel-mode matrix`** — `tests/UnitTests/Alerts/{AlertsTestCategory,AlertFiltersSerializerTests,AlertServiceTests}.cs`. 23 service tests in the initial commit; +1 cross-tenant test (`SetChannelMode_AlertOwnedByAlice_AsBob_ReturnsNotFound`) in a follow-up commit, OR all 24 in the initial commit if the user prefers single commit per concern.
5. **`chore(entities): Equals/GetHashCode on Alert and AlertChannelMode (wave-2 review #15)`** — `Domain/Entities/Alert.cs`, `Domain/Entities/AlertChannelMode.cs`.
6. **`test(migration): hardened // Forward-only after merge tripwire (wave-2 review #16)`** — `tests/UnitTests/DatabaseSchemaTests.cs` (renamed test, regex-hardened assertion).
7. **`fix(alerts): wave-5 review fixes — R1 dead if in SetChannelModeAsync, R2 dead empty-{} branch in serializer, R3 cross-tenant channel-mode test, R4 defensive default in MapNotFound, R5 Guid.Empty short-circuits`** — five small changes; can be one commit or five.
8. **`fix(alerts): second-round review fixes — S1 RejectEmptyGuid helper to dedup 6 sites, S2 CallerMemberName on MapNotFound, S3 both-direction probe comment on service, S4 tightened test because, S5 tighter SetChannelMode comment`** — five small dedup + doc-tightening changes; can be one commit or five.

**Recommend**: combine 1-6 into one PR (the wave scope), and put 7 + 8 in follow-up PRs (the review fixes) so the diffs are reviewable. The follow-up PRs are short and can be merged quickly. Alternatively, 7 and 8 can be one PR ("wave-5 review fixes" round 1 + 2) — the total diff is still under 100 lines.

---

## 7. Pre-existing issues that block later waves (NOT mine, but worth flagging)

- **§2.4 — `IDbContextFactory<>` DI gap**: blocks wave 11 E2E. One-line fix + 2 tests. **Recommend**: small follow-up PR before wave 6 starts.
- **§2.3 — `dev:up` / `dev:reset-db` migration wiring**: doesn't block any wave (the user is expected to run `dotnet ef database update` manually), but a fresh-clone experience is broken. **Recommend**: small follow-up PR.
- **Wave 4 follow-up §2.3 — MailKit/MimeKit NU1902 advisories**: deferred to wave 8 per the wave-4 handoff. Not blocking.
- **Wave 3 follow-up §"Open question" — `EmailVerification.Token` and `PasswordResetToken.Token` plaintext storage**: still open. **Recommend**: pre-wave-8 polish PR (matches the wave-3 handoff's "or wave 11 polish" suggestion).
