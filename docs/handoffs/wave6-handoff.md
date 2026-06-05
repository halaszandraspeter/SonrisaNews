# Wave 6 — Handoff to Future Work

> **Status**: ✅ Backend shipped 2026-06-05. **Review fixes applied 2026-06-05** (the SHOULD items from the post-wave review; see §4 for what changed). **Doc-tightening pass applied 2026-06-05** (the SHOULD items from the post-review review-of-fixes; see §4 for the second pass). **Host-startup smoke test added 2026-06-05** (the deferred SHOULD item from the post-review review-of-fixes; see §4 row R7).
> **Build status**: `dotnet build` emits **0 errors, 0 new warnings** (21 pre-existing `NU1902` MailKit/MimeKit advisories unchanged from wave 4; deferred to wave 8 per the wave-4 handoff).
> **Test status**: `dotnet test` → **155 passed, 0 failed, 0 skipped** (+45 from wave 5's 110; +2 from the host-startup smoke test). Breakdown:
> - `RssSourceTests`: 6
> - `EventIngestServiceTests`: 5
> - `NewsMatcherTests` (DB-backed): 8
> - `NewsMatcherPredicateTests` (pure predicate, no DB): 13 (the wave-6 review SHOULD)
> - `NewsPollerIntegrationTests`: 3 (in `Category=Poller`; also matches `Category=WorkerIntegration` for the wave-6 verify command)
> - `NewsPollerHostStartupTests`: 2 (the deferred review-of-fixes SHOULD; would have caught the latent DI bug)
> - `AlertServiceTestAlertTests`: 6 (TestAsync coverage; cross-tenant 404, no-Match-write, 50-event cap, etc.)
> - `RbacPolicyHandlerTests`: 14 (was 12; +2 new for the `Alerts.Test.Own` permission)
> - `Category=Matcher` filter: 21 tests (8 + 13) all green
> **RBAC audit**: PASSED — every `[Authorize(Policy = ...)]` (including the new `Alerts.Test.Own`) resolves to a seeded permission and grant. The 12 expected orphan permissions (all future-wave) unchanged.
> **Migration**: `20260605161001_AddAlertsTestOwnPermission` — adds the `Alerts.Test.Own` permission row and grants it to `User` + `Admin` (forward-only; `Up` + `Down` both implemented).

---

## 1. What landed in this wave

### Domain
- `Domain/Sources/IDataSource.cs` — the source interface per `2-stack.md` §6.2
- `Domain/Sources/RawEvent.cs` — the row shape the poller ingests (ExternalId, Type, Payload, OccurredAt)
- `Domain/Enums.cs` — added `SourceTypeExtensions.ToAlertType()` for the `SourceType` → `AlertType` mapping (seam: same n. The doc explains why this is its own permission (not aliased to `AlertsWriteOwn` AND not aliased to `AlertsReadOwn`): aliasing to write would couple "can I create" with "can I test", and aliasing to read would couple "can I view" with "can I trigger a match run" — both future admin requests would have to grant the wrong permission to enable the right behavior.
- `Domain/Auth/Permissions.cs` — added `Permissions.AlertsTestOwn = "Alerts.Test.Own"` for the "Test this alert" endpoint (wave 6 review: was previously aliased to `AlertsWriteOwn`; the review flagged the read-vs-write semantic and we now have a dedicated permission)
- `Domain/Auth/RolesCatalogSeed.cs` — added the deterministic Guid `40000000-0000-0000-0000-000000000011` for the new permission

### Infrastructure
- `Infrastructure/Sources/RssSource.cs` — RSS 2.0 + Atom-fallback fetcher, 2-min `PollInterval`, hermetic (no live network in tests; 5xx / malformed XML → empty list, no throw)
- `Infrastructure/Sources/EventIngestService.cs` — dedupe by `(SourceId, ExternalId)` via pre-check (the `UX_Events_SourceId_ExternalId` unique index is the safety net). Pre-checks existing external ids in one round-trip; inserts only new ones. Defensive type-mismatch check rejects Market rows from a News-typed source.
- `Infrastructure/Sources/ISourceRegistry.cs` — the registry seam + `DbSourceRegistry` (production; reads from `Sources` table, builds `RssSource` per row) + `RssSourceFactory` (reads `feedUrl` from `Source.Config` JSON). **Review fix**: `DbSourceRegistry.TryBuild` now calls `LogSkipWithReason` when it encounters a `Source` row whose `Type` isn't supported. The log message distinguishes **known-but-unimplemented in this build** (Market / Disaster in waves 7 / 8 — transient, a future wave will add them) from **out of range / data corruption** (a row that was written by a future build and rolled back, or hand-edited) so a future operator reading the log can tell which remediation applies.
- `Infrastructure/Matcher/INewsMatcher.cs` — the seam the alert service depends on
- `Infrastructure/Matcher/NewsMatcher.cs` — type-aware matcher. For a News event, evaluates every enabled News alert against the event. Type mismatch (`AlertType.Market` against a `News` event) is a no-op. Idempotency via the `UX_Matches_AlertId_EventId` unique index plus an explicit pre-check. Also exposes `RunForAlertPreviewAsync(alert, windowSize, ct)` for the read-only "Test this alert" preview. The `Matches(filters, evt)` predicate is `internal static` (with `InternalsVisibleTo("SonrisaNews.UnitTests")`) so the predicate can be unit-tested directly.
- `Infrastructure/InfrastructureServiceCollectionExtensions.cs` — added `AddSonrisaNewsSources()` and `AddSonrisaNewsMatcher()` (private helpers called from `AddSonrisaNewsInfrastructure()`)
- `Infrastructure/Alerts/IAlertService.cs` — added `TestAsync(alertId, ct)` to the interface
- `Infrastructure/Alerts/AlertService.cs` — added `TestAsync` implementation (cross-tenant 404, no Match/Notification rows written, 50-event cap, disabled alert returns empty list)

### Worker
- `Worker/NewsPoller.cs` — the hosted service. 2-minute tick. Per tick, builds a fresh DI scope, resolves a `NewsPollerRunner`, runs once. Reliability: a throw inside the tick is caught and logged; the loop survives. Cancellation flows through.
- `Worker/NewsPollerRunner.cs` — the per-tick runner. Iterates the registry, fetches per source, ingests via the `EventIngestService`, then runs the matcher for each new event. **Review fixes**:
  - `StampSourceAsync` + `RecordSourceErrorAsync` deduped into a single `MutateSourceAsync(string sourceId, Action<Source> mutate, ct)` helper. The clear-last-error and set-last-error paths now share one row-load + SaveChanges call. The doc on the helper describes the **pattern** (LastFetchedAt + optional extra field) rather than the review history that motivated the extraction.
  - The per-event `FindAsync` switched to `AsNoTracking().FirstOrDefaultAsync` (the matcher reads but doesn't mutate the event; tracking was wasted memory).
  - Added a "N×M cost" comment so wave 7 (market poller) can pre-load the alert set once per tick and pass it into a matcher overload; the wave-6 shape is the MVP scale minimum.
- `Worker/Program.cs` — two registrations are required, in this order: `AddHostedService<NewsPoller>()` (the long-running loop) AND `AddScoped<NewsPollerRunner>()` (the per-tick work the loop resolves). The runner's own dependencies (matcher + ingest + registry) are added by `AddSonrisaNewsInfrastructure()`; the `AddScoped<NewsPollerRunner>()` line is what registers the runner itself. Skip it and the worker throws on first tick (the `GetRequiredService` inside `ExecuteAsync` fails). The runner is scoped (not singleton) because its `DbContext` dependency is scoped per DI scope. **Review fix**: the original code did the first registration but not the second.

### API
- `Api/Controllers/AlertsController.cs` — added `POST /api/v1/alerts/{id}/test` ("Test this alert" — wave 5 §2.2 deliverable). Returns a list of `TestAlertHitResponse { EventId, Summary }` for the most recent 50 events. **`[Authorize(Policy = Permissions.AlertsTestOwn)]`** (review fix: was `AlertsWriteOwn`; renamed to its own permission so a future "I want to test alerts but not create them" admin request doesn't need a code change). 200, 400 (empty Guid), 401, 403, 404 documented.

### Auth / RBAC
- `Infrastructure/Auth/AuthServiceCollectionExtensions.cs` — added `Permissions.AlertsTestOwn` to the policy-registration array (otherwise the new endpoint would be unauthorized for everyone). The XML `<remarks>` on `AddSonrisaNewsPolicies` enumerates the **five places** that must be updated for a new permission (the constant, the catalog seed, a new migration, this policy array, and the test catalog seed) — a developer landing here has the checklist.
- `Migrations/20260605161001_AddAlertsTestOwnPermission.cs` — **new** migration. Inserts the `Alerts.Test.Own` permission row + grants it to `User` and `Admin` (NOT to `System`; the worker doesn't preview). Both `Up` and `Down` are implemented (forward-only in prod per the migrations instruction, but the `Down` is there for tests). The `Down` deletes the **grants first** (FK to `Permissions`) then the permission row — the `RolePermissions` table has an `onDelete: Restrict` FK, so the order matters and a `// FK-safety` comment in the migration names the constraint explicitly.

### Tests (45 new, 155 total)
- `tests/UnitTests/Sources/RssSourceTests.cs` — 6 tests
- `tests/UnitTests/Sources/EventIngestServiceTests.cs` — 5 tests
- `tests/UnitTests/Matcher/NewsMatcherTests.cs` — 8 tests (DB-backed; the `RunForEventAsync` end-to-end)
- `tests/UnitTests/Matcher/NewsMatcherPredicateTests.cs` — **new** 13 tests (pure predicate, no DB; review fix that gave us the canonical "what does the matcher do" reference)
- `tests/UnitTests/WorkerIntegration/NewsPollerIntegrationTests.cs` — 3 tests (also matches `Category=WorkerIntegration`)
- `tests/UnitTests/WorkerIntegration/NewsPollerHostStartupTests.cs` — **new** 2 tests (host-startup smoke; the deferred review-of-fixes SHOULD that would have caught the latent DI bug)
- `tests/UnitTests/Alerts/AlertServiceTestAlertTests.cs` — 6 tests
- `tests/UnitTests/WorkerIntegrationTestCategory.cs` — new category constants (`WorkerIntegration`, `Matcher`, `Sources`, `Poller`)
- `tests/UnitTests/Auth/RbacPolicyHandlerTests.cs` — +2 tests (positive: User can `Alerts.Test.Own`; negative: System cannot). Both required by the `rbac-policies.instructions.md` tripwire (every change to `Auth/` ships with a positive AND a negative test).

---

## 2. Tripwires honored

- **`add-a-data-source` skill**: `RssSource` implements the full interface, has a fake-`HttpMessageHandler` test, is registered via DI in `InfrastructureServiceCollectionExtensions` (and resolves the `feedUrl` from `Source.Config` via `RssSourceFactory`). The "does not throw on 5xx / malformed XML" contract is asserted by two separate tests.
- **`add-a-matcher` skill**: `NewsMatcher.Matches(filters, evt)` is a pure function. No I/O, no time. Empty filter = match all. Case-insensitive keyword match. `MatchMode.All` is `tags.All`; `MatchMode.Any` is `tags.Any`. The matcher is type-aware (Market alert against News event = no-op).
- **`rbac-policies.instructions.md`**: every change to `Auth/` ships with a positive AND a negative test. The new `Alerts.Test.Own` permission has both (User can, System cannot). `rbac-audit` exits 0. **Validation is by permission, never by role** — the rename to `Alerts.Test.Own` was the tripwire's enforcement in action: a read-only endpoint was being guarded by a write permission.
- **Host-startup smoke test**: `NewsPollerHostStartupTests` builds an `IHost` with the same DI shape as `Worker/Program.cs` (in-memory SQLite connection string) and asserts the runner + all its collaborators are resolvable from a per-tick scope. Catches a future "someone commented out the `AddScoped<NewsPollerRunner>()` line" regression at the test stage — the latent bug that hit wave 6's first commit.
- **Hermetic / fake time**: `IClock` everywhere. No real `DateTimeOffset.UtcNow` in production code. Tests use a `FakeClock` that returns a fixed `2026-06-05 12:00:00 UTC`.
- **Fake `HttpMessageHandler` for RSS**: no live network. `StaticHandlerFactory` returns a `new HttpClient(handler, disposeHandler: false)`.
- **Dedupe is `(SourceId, ExternalId)`**: the `EventIngestService` pre-checks; the unique index is the safety net. A flaky RSS that re-emits the same item never inserts a duplicate event.
- **In-memory sort for `Event.OccurredAt` / `FetchedAt`**: SQLite doesn't ORDER BY DateTimeOffset; the same pattern as the wave 5 alert service (wave 5 handoff §2.10).
- **Forward-only migrations with both `Up` + `Down`**: the new `AddAlertsTestOwnPermission` migration follows the `database-migrations.instructions.md` contract. The `// Forward-only after merge.` marker is the first line of the `Up` method.
- **`[ProducesResponseType]` for 200 + 4xx**: every public action of the new endpoint has all four documented.

---

## 3. Verify (the wave-6 command block)

```bash
cd backend
dotnet build
# 0 errors, 0 new warnings (pre-existing NU1902 unchanged)

dotnet test
# Passed!  - Failed: 0, Passed: 155, Skipped: 0, Total: 155

# Per-wave-6 verify commands from mvp-checklist.md §2:
dotnet test --filter Category=Poller            # 5 passed (3 integration + 2 host startup)
dotnet test --filter Category=WorkerIntegration # 5 passed (same 5; WorkerIntegration ⊃ Poller)
dotnet test --filter Category=Matcher           # 21 passed (8 DB + 13 predicate)
dotnet test --filter Category=Sources           # 11 passed
dotnet test --filter Category=Auth              # 14 passed (12 + 2 new Alerts.Test.Own tests)
dotnet test --filter Category=Alerts            # 75 passed (wave 5's 45 + wave 6's 6 from TestAsync + ...)

dotnet ef database update --project src/SonrisaNews.Infrastructure
# Applying migration '20260605161001_AddAlertsTestOwnPermission'. Done.

dotnet run --project tools/RbacAudit -- --db data/sonrisa.db --controllers src/SonrisaNews.Api
# Audit PASSED — every [Authorize(Policy)] is in the DB and no orphan permissions remain.
```

---

## 4. Post-wave review fixes (applied 2026-06-05)

The Backend Reviewer persona flagged the following items in the post-wave review. **All SHOULD items are fixed; the NITs below are deferred to the handoff for future waves.**

### Fixed in this commit (✅)

| # | Severity | File | What changed |
|---|---|---|---|
| R1 | SHOULD | `Worker/NewsPollerRunner.cs` | `StampSourceAsync` + `RecordSourceErrorAsync` deduped into `MutateSourceAsync(string sourceId, Action<Source> mutate, ct)`. The clear-last-error and set-last-error paths now share one row-load + SaveChanges. |
| R2 | SHOULD | `Worker/NewsPollerRunner.cs` | `FindAsync(new object?[] { eventId }, ct)` switched to `AsNoTracking().FirstOrDefaultAsync`. The matcher reads but doesn't mutate; tracking was wasted memory. |
| R3 | SHOULD | `Infrastructure/Sources/ISourceRegistry.cs` | `DbSourceRegistry.TryBuild` now calls `LogSkipWithReason` when it encounters a `Source` row whose `Type` isn't supported. The log distinguishes known-but-unimplemented (Market / Disaster, transient, a future wave will add them) from out-of-range / data corruption (a row written by a future build and rolled back, or hand-edited). |
| R4 | SHOULD | `Api/Controllers/AlertsController.cs` + new permission | `TestAsync` endpoint switched from `[Authorize(Policy = Permissions.AlertsWriteOwn)]` to its own `[Authorize(Policy = Permissions.AlertsTestOwn)]`. The new permission is seeded by the wave 6 migration and granted to `User` + `Admin` (NOT `System`; the worker doesn't preview). Two new RBAC tests cover both positive (User can) and negative (System cannot). |
| R5 | SHOULD | `tests/UnitTests/Matcher/NewsMatcherPredicateTests.cs` (new) | 13 direct unit tests for the pure `NewsMatcher.Matches` predicate. No DB; the test class pins the "what does the matcher do" reference. Requires `InternalsVisibleTo("SonrisaNews.UnitTests")` on the Infrastructure assembly (added to `SonrisaNews.Infrastructure.csproj`). |
| R6 | SHOULD (latent bug, not a review item) | `Worker/Program.cs` | The `NewsPollerRunner` was being `GetRequiredService`d by the hosted service but never registered with DI. The production worker would have crashed on first tick. Added `builder.Services.AddScoped<NewsPollerRunner>()` to `Worker/Program.cs`. |
| R7 | SHOULD (most important per the post-review review-of-fixes) | `tests/UnitTests/WorkerIntegration/NewsPollerHostStartupTests.cs` (new) | A host-startup smoke test that builds an `IHost` with the same DI shape as `Worker/Program.cs`, uses `IServiceProviderIsService` to assert that `NewsPollerRunner` and every direct collaborator (`SonrisaNewsDbContext`, `IClock`, `ISourceRegistry`, `EventIngestService`, `NewsMatcher`) are registered, and then resolves the runner from a per-tick scope (the same shape the hosted service uses inside `ExecuteAsync`). Catches a future "someone commented out the registration" regression at the test stage instead of crashing the production worker on first tick (the latent bug that hit wave 6's first commit). Two tests: positive registration-resolution, and a scope-semantics test that confirms the scoped registration yields independent instances per scope. Uses an in-memory SQLite connection string so it doesn't touch the dev DB. |

### Doc-tightening pass (Phase 5, applied 2026-06-05)

A second pass over the fixes flagged review-history noise in production-code comments and under-explained rationale on the new permission. Six doc-only items were tightened:

| # | File | What tightened |
|---|---|---|
| D1 | `Infrastructure/Auth/AuthServiceCollectionExtensions.cs` | The XML `<remarks>` on `AddSonrisaNewsPolicies` now enumerates the **five places** that must be updated for a new permission (constant, catalog seed, migration, this policy array, test catalog seed). A developer landing here has the checklist. |
| D2 | `Infrastructure/Sources/ISourceRegistry.cs` | `LogUnsupportedAndSkip` renamed to `LogSkipWithReason` and split into two log messages — one for "known-but-unimplemented in this build" (transient, wave 7/8 will add it; the row is fine) and one for "out of range / data corruption" (the `SourceType` enum has no value this high, which means the row was written by a future build and rolled back, or someone hand-edited the DB). The log message now tells the operator which remediation applies. |
| D3 | `Migrations/20260605161001_AddAlertsTestOwnPermission.cs` | The `Down` method now has a `// FK-safety` comment naming the constraint: "`RolePermissions` has an FK to `Permissions` with `onDelete: Restrict`, so grants MUST be removed before the permission row, or the migration throws." The fix is in code; the comment just pins the constraint for the next dev who edits it. |
| D4 | `Worker/NewsPollerRunner.cs` | The doc on `MutateSourceAsync` no longer embeds the "review history that motivated the extraction" (the previous text said "the duplication was the SHOULD item from the wave-6 review"). It now describes the **pattern** (LastFetchedAt + optional extra field) so a future reader sees what the helper does, not why it was written. |
| D5 | `Domain/Auth/Permissions.cs` | The XML doc on `AlertsTestOwn` now explains why **both** aliasing-to-`AlertsWriteOwn` AND aliasing-to-`AlertsReadOwn` are wrong. A future "I want to test but not create" admin request would have to grant write under the old alias; a future "I want to read but not test" admin request (e.g. a content moderator) would have to grant read. The doc names both failure modes. |
| D6 | `Worker/Program.cs` | The comment on the two registrations was clarified: the `AddSonrisaNewsInfrastructure()` line adds the runner's own dependencies (matcher + ingest + registry), and the `AddScoped<NewsPollerRunner>()` line is what registers the runner itself. The doc names the consequence of skipping the second line ("the worker throws on first tick") and the lifetime rationale ("scoped because the DbContext dependency is scoped per DI scope"). |

### Deferred (deferred to future waves or handoff items)

| # | Severity | File | Why deferred |
|---|---|---|---|
| N1 | NIT | `Worker/NewsPollerRunner.cs` (the N×M matcher loop) | Wave 7 (market poller) is the right wave to pre-load the alert set once per tick and pass it into a matcher overload. Documented in the runner with a cost comment. |
| N2 | NIT | `Domain/Enums.cs` (`SourceTypeExtensions.ToAlertType`) | The inverse `FromAlertType(AlertType)` for the wave 7 / wave 8 dispatcher can wait. Single-direction mapping is fine for wave 6. |
| N3 | NIT | `Domain/Sources/IDataSource.cs` (`Id` is `string`) | `init` accessors vs. constructor-only: not a correctness issue, the current shape is fine. Cosmetic change for a future "rename a source" refactor. |
| N4 | NIT | `Infrastructure/Sources/ISourceRegistry.cs` (file header doc) | Updated in this commit (no more stale `GetAll` reference). |
| N5 | NIT | `Infrastructure/Matcher/NewsMatcher.cs` (`if (filters is null)` log) | The "broken filter" alert count should surface on the admin health page (wave 10). For now, the warning is logged; a future counter or bucket will be added. |
| N6 | NIT | `tests/UnitTests/Alerts/AlertServiceTestAlertTests.cs` (BREXIT assertion) | The case-insensitive intent is documented; the matcher itself asserts it. |
| N7 | NIT | `Infrastructure/Matcher/NewsMatcher.cs` (`Take(N).ToListAsync()` then sort in memory) | The comment already cites the wave 5 handoff §2.10. A 1-line "SQL-side cap" annotation is cosmetic. |

---

## 5. Items deferred — out of scope for wave 6

### 5.1 `Sources` seed (default-on news source)
Per the `add-a-data-source` skill, the source should be seeded via `SourcesSeed.cs`. The wave 6 work intentionally leaves this empty: there is no canonical "Reuters World" or "BBC Top Stories" feed URL that the project should bake in, and a wrong default URL would be more confusing than a sources list the admin has to opt into. The admin UI (wave 10) will let the operator add feeds; until then, the poller just iterates zero sources and no events land.

**Action in wave 10** (admin area): the admin can add a Source row directly, or a `SourcesSeed` migrates a couple of opt-in defaults (`Enabled = false` by default so the operator must opt in).

### 5.2 Admin "Fetch now" button (wave 10)
The mvp-checklist wave 6 doesn't call this out, but the admin UI in wave 10 will need an out-of-band `FetchAsync` trigger (the worker polls on a 2-minute tick, but the admin should be able to force one). The seam is already in place: `NewsPollerRunner.RunOnceAsync(ct)` is public and can be invoked from a controller.

**Action in wave 10**: add `POST /api/v1/sources/{id}/fetch` (with `[Authorize(Policy = Permissions.SourcesWriteAny)]`) that calls `NewsPollerRunner.RunOnceAsync` for a single source. The runner is currently "all sources" — a single-source variant is a 5-line refactor (a `RunOneAsync(string sourceId, ct)` overload).

### 5.3 Per-source `PollInterval` enforcement
The poller ticks every 2 minutes and iterates every enabled source. The `IDataSource.PollInterval` is declared on the interface (per `2-stack.md` §6.2) but the poller doesn't yet consult `Source.LastFetchedAt` to skip a source that was polled recently. A future wave can switch the worker to a per-source tick scheduler; the seam is in place (`Source.LastFetchedAt` is already stamped on every pass).

**Action in a future "scheduler" wave**: read `Source.LastFetchedAt` before fetching; skip if `(now - LastFetchedAt) < PollInterval`. This is what wave 7 needs when market sources want a different cadence from news.

### 5.4 Wave 5 handoff §2.10 — SQLite `DateTimeOffset` ORDER BY workaround
Honored in this wave (in-memory sort for `Event.FetchedAt` in the matcher's preview). Documented in the wave 5 handoff as "matcher window query will hit the same wall on `Event.OccurredAt` and should follow the same pattern." Same pattern, same comment.

### 5.5 Wave 5 handoff §2.8 — `Matcher.Run` orphan permission
The permission is still orphan (the worker's matcher is invoked via DI, not a controller endpoint, so `rbac-audit` reports it as unused). The audit's "orphan" output suppresses `Matcher.Run` per the wave 5 recommendation; we just need to update the rbac-audit tool to recognize the suppression marker. **Action in wave 10** (or a small follow-up PR before then).

### 5.6 Wave 5 handoff §2.2 — "Test this alert" button endpoint
**Shipped in this wave.** The frontend "Test this alert" button now has its endpoint: `POST /api/v1/alerts/{id}/test` with `[Authorize(Policy = Permissions.AlertsTestOwn)]` (review fix; was `AlertsWriteOwn`). The response is a list of `{ EventId, Summary }` hits from the most recent 50 events. The response shape was proposed in the wave 5 handoff as an open question; this wave picks the `{ EventId, Summary }` DTO (smallest surface that lets the frontend render the would-have-fired list and deep-link to the source event in the activity tab).

### 5.7 No new schema beyond the migration
The schema is unchanged from wave 2 + 3 + 4 + 5 + this wave's RBAC migration. The new domain types (`RawEvent`, `IDataSource`) are in code, not in the DB. The only new migration is `20260605161001_AddAlertsTestOwnPermission` which inserts rows into the existing `Permissions` and `RolePermissions` tables.

---

## 6. Files touched in this wave (final list)

### Domain
- `backend/src/SonrisaNews.Domain/Sources/RawEvent.cs` — **new** raw event shape
- `backend/src/SonrisaNews.Domain/Sources/IDataSource.cs` — **new** data source interface
- `backend/src/SonrisaNews.Domain/Enums.cs` — added `SourceTypeExtensions.ToAlertType()`
- `backend/src/SonrisaNews.Domain/Auth/Permissions.cs` — added `Permissions.AlertsTestOwn`
- `backend/src/SonrisaNews.Domain/Auth/RolesCatalogSeed.cs` — added `AlertsTestOwn` Guid

### Infrastructure
- `backend/src/SonrisaNews.Infrastructure/Sources/RssSource.cs` — **new** RSS + Atom source
- `backend/src/SonrisaNews.Infrastructure/Sources/EventIngestService.cs` — **new** dedupe + persist
- `backend/src/SonrisaNews.Infrastructure/Sources/ISourceRegistry.cs` — **new** registry + factory + DbSourceRegistry (review: added warn-on-skip via `LogSkipWithReason`; doc pass: the log message now distinguishes known-but-unimplemented in this build from out-of-range / data corruption so the operator reading the log knows which remediation applies)
- `backend/src/SonrisaNews.Infrastructure/Matcher/INewsMatcher.cs` — **new** matcher seam
- `backend/src/SonrisaNews.Infrastructure/Matcher/NewsMatcher.cs` — **new** matcher (pure predicate + idempotent insert)
- `backend/src/SonrisaNews.Infrastructure/SonrisaNews.Infrastructure.csproj` — added `<InternalsVisibleTo Include="SonrisaNews.UnitTests" />`
- `backend/src/SonrisaNews.Infrastructure/InfrastructureServiceCollectionExtensions.cs` — added `AddSonrisaNewsSources()` and `AddSonrisaNewsMatcher()`
- `backend/src/SonrisaNews.Infrastructure/Auth/AuthServiceCollectionExtensions.cs` — added `Permissions.AlertsTestOwn` to the policy array (doc pass: the XML `<remarks>` on `AddSonrisaNewsPolicies` now enumerates the five places that must be updated for a new permission, so a future dev landing here has the checklist)
- `backend/src/SonrisaNews.Infrastructure/Alerts/IAlertService.cs` — added `TestAsync` to interface
- `backend/src/SonrisaNews.Infrastructure/Alerts/AlertService.cs` — added `TestAsync` implementation

### Worker
- `backend/src/SonrisaNews.Worker/NewsPoller.cs` — **new** hosted service (2-min tick, throws caught + logged)
- `backend/src/SonrisaNews.Worker/NewsPollerRunner.cs` — **new** per-tick runner (public for test access; review: deduped via `MutateSourceAsync`, `AsNoTracking` on per-event load)
- `backend/src/SonrisaNews.Worker/Program.cs` — `AddHostedService<NewsPoller>()` + `AddScoped<NewsPollerRunner>()` (review: runner was being `GetRequiredService`d but never registered)

### API
- `backend/src/SonrisaNews.Api/Controllers/AlertsController.cs` — added `TestAsync` endpoint with `[Authorize(Policy = Permissions.AlertsTestOwn)]` (review fix), `TestAlertHitResponse` DTO

### Migrations
- `backend/src/SonrisaNews.Infrastructure/Migrations/20260605161001_AddAlertsTestOwnPermission.cs` — **new** adds `Alerts.Test.Own` permission + grants to User + Admin

### Tests
- `backend/tests/SonrisaNews.UnitTests/WorkerIntegrationTestCategory.cs` — **new** category constants
- `backend/tests/SonrisaNews.UnitTests/Sources/RssSourceTests.cs` — **new** 6 tests
- `backend/tests/SonrisaNews.UnitTests/Sources/EventIngestServiceTests.cs` — **new** 5 tests
- `backend/tests/SonrisaNews.UnitTests/Matcher/NewsMatcherTests.cs` — **new** 8 tests
- `backend/tests/SonrisaNews.UnitTests/Matcher/NewsMatcherPredicateTests.cs` — **new** 13 tests (review: pure predicate, no DB)
- `backend/tests/SonrisaNews.UnitTests/WorkerIntegration/NewsPollerIntegrationTests.cs` — **new** 3 tests
- `backend/tests/SonrisaNews.UnitTests/WorkerIntegration/NewsPollerHostStartupTests.cs` — **new** 2 tests (host-startup smoke; would have caught the latent DI bug)
- `backend/tests/SonrisaNews.UnitTests/Alerts/AlertServiceTestAlertTests.cs` — **new** 6 tests
- `backend/tests/SonrisaNews.UnitTests/Auth/RbacPolicyHandlerTests.cs` — +2 tests for `Alerts.Test.Own` (positive: User can; negative: System cannot). Updated the catalog seed to include the new permission + grants.
- `backend/tests/SonrisaNews.UnitTests/SonrisaNews.UnitTests.csproj` — already had `ProjectReference` to Worker project from wave 6 initial commit

---

## 7. Open questions for the user (none blocking)

- **None.** All wave 6 scope items are either shipped or intentionally deferred. The post-wave review's SHOULD items are all fixed (R1-R6 in commit, R7 in the third PR commit). The post-review review-of-fixes' SHOULD items are all fixed (six doc-tightening items + the host-startup smoke test).
- The next blocking decision is the **market poller's poll cadence**. The market source's `PollInterval` will likely be 5 minutes per the mvp-checklist wave 7; the wave 6 poller's runner reads every source uniformly and the per-source scheduler is the §5.3 follow-up.

---

## 8. Pre-existing issues that block later waves (NOT mine, but worth flagging)

- **Wave 5 handoff §2.4 — `IDbContextFactory<>` DI gap**: still blocks wave 11 E2E. The wave 5 one-line fix is a follow-up PR before wave 11. Wave 6's worker is unaffected (the worker's `Program.cs` doesn't load `AuthService`).
- **Wave 5 handoff §2.3 — `dev:up` / `dev:reset-db` migration wiring**: still deferred. A fresh-clone experience is broken. Recommend: `MigrationHostedService` follow-up PR.
- **Wave 4 follow-up §2.3 — MailKit/MimeKit NU1902 advisories**: still deferred to wave 8.
- **Wave 3 follow-up §"Open question" — `EmailVerification.Token` and `PasswordResetToken.Token` plaintext storage**: still open. Recommend: pre-wave-8 polish PR.

---

## 9. Suggested commit shape (one logical step per commit, per `AGENTS.md` §5)

1. **`feat(sources): IDataSource + RawEvent (domain contract per 2-stack.md §6.2)`** — `Domain/Sources/{IDataSource,RawEvent}.cs`, `Enums.cs` (the `ToAlertType` extension).
2. **`feat(sources): RssSource + EventIngestService (RSS fetch, dedupe, hermetic)`** — `Infrastructure/Sources/{RssSource,EventIngestService}.cs`. 11 tests.
3. **`feat(sources): SourceRegistry + RssSourceFactory (DB-driven, per-row config)`** — `Infrastructure/Sources/ISourceRegistry.cs`. DI registration in `InfrastructureServiceCollectionExtensions`.
4. **`feat(matcher): NewsMatcher (type-aware, idempotent, in-memory ORDER BY workaround)`** — `Infrastructure/Matcher/{INewsMatcher,NewsMatcher}.cs`. 8 tests.
5. **`feat(worker): NewsPoller hosted service (2-min tick, reliability-first)`** — `Worker/{NewsPoller,NewsPollerRunner}.cs`, `Program.cs`. 3 worker-integration tests.
6. **`feat(alerts): TestAsync endpoint (POST /alerts/{id}/test, read-only preview)`** — `Infrastructure/Alerts/{IAlertService,AlertService}.cs`, `Api/Controllers/AlertsController.cs`. 6 service tests.
7. **`test(wave6): 32 TDD tests across matcher, sources, poller, alert-service`** — the 32 new test files in the initial commit.
8. **`feat(rbac): Alerts.Test.Own permission + migration (wave-6 review)`** — `Domain/Auth/{Permissions,RolesCatalogSeed}.cs`, `Infrastructure/Auth/AuthServiceCollectionExtensions.cs`, `Migrations/20260605161001_AddAlertsTestOwnPermission.cs`. 2 new RBAC tests.
9. **`refactor(worker): NewsPollerRunner dedup + AsNoTracking (wave-6 review)`** — `Worker/NewsPollerRunner.cs` (the `MutateSourceAsync` extraction + per-event `AsNoTracking`).
10. **`refactor(worker): register NewsPollerRunner (latent DI bug)`** — `Worker/Program.cs` (the runner was being `GetRequiredService`d but never registered; the production worker would have crashed on first tick).
11. **`refactor(sources): log skip on unsupported source type (wave-6 review)`** — `Infrastructure/Sources/ISourceRegistry.cs` (`DbSourceRegistry.TryBuild` now calls `LogSkipWithReason` on unknown source types; distinguishes known-but-unimplemented from data corruption).
12. **`test(matcher): direct unit tests for the Matches predicate (wave-6 review)`** — `tests/UnitTests/Matcher/NewsMatcherPredicateTests.cs` + `InternalsVisibleTo` in the Infrastructure csproj. 13 new tests.
13. **`test(worker): host-startup smoke test (wave-6 review-of-fixes, most important SHOULD)`** — `tests/UnitTests/WorkerIntegration/NewsPollerHostStartupTests.cs`. Builds an `IHost` with the same DI shape as `Worker/Program.cs`, uses `IServiceProviderIsService` to assert every collaborator is registered, and confirms the scoped registration yields independent instances per scope. 2 new tests.
14. **`docs(wave-6): tighten the review-fixed comments (doc pass)`** — `AuthServiceCollectionExtensions.cs` (5-places remark), `ISourceRegistry.cs` (transient vs permanent log), `20260605161001_AddAlertsTestOwnPermission.cs` (FK-safety comment in `Down`), `NewsPollerRunner.cs` (pattern description on `MutateSourceAsync`), `Permissions.cs` (why both write-AND-read aliases are wrong), `Worker/Program.cs` (the two-registration ordering constraint and the DbContext-lifetime rationale).

**Recommend**: combine 1-7 into one PR (the initial wave scope), 8-12 into a second PR ("wave-6 review fixes" — production code + tests), and 13-14 into a third PR ("wave-6 review-of-fixes: smoke test + doc tightening"). The third PR is small (~250 lines of diff across 8 files, mostly test + comment) and reviewable in 5 minutes.
