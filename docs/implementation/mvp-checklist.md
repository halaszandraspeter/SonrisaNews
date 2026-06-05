# Sonrisa News — MVP Implementation Checklist

> **Scope**: the 24-hour build. Source of truth: [`1-features.md`](../roadmap/1-features.md), [`2-stack.md`](../roadmap/2-stack.md), [`3-ai-environment.md`](../roadmap/3-ai-environment.md).
> **Status**: planning + bootstrap done. **Waves 1, 2, 3, 4, and 5 shipped between 2026-06-04 and 2026-06-05.** The build-order table below shows per-wave status. This doc tracks the build order, the audit of the current repo, and the tripwires that apply to every step.
> **Last updated**: 2026-06-05 (wave 6 — news poller + matcher — marked shipped; see timeline note below).
> **Timeline note**: work on this checklist was paused after wave 4 (the user ran out of time). The wave 5 (alerts + filters) and wave 6 (news poller + matcher) work was picked up and shipped afterwards, with wave 5 shipped out of strict wave order. The wave table and status sections below reflect what actually shipped, not the original 24-hour timeline.
> **User has final word** on every decision below. If a wave is too large, the wave splits; if a wave is too small, the wave merges.

---

## 0. Audit of the current repo

Before we start, here's what's already in place vs. what the docs claim. **No code is written yet** — the harness is the only thing that exists.

### 0.1 Present ✅

| Area | File(s) | Status |
|---|---|---|
| 3 locked planning docs | `docs/roadmap/{1-features,2-stack,3-ai-environment}.md` | Final. All decisions locked. |
| Project-wide ground rules | `AGENTS.md` | Final. |
| Copilot-specific hooks | `.github/copilot-instructions.md` | Final. |
| Per-domain instructions (8) | `.github/instructions/*.instructions.md` | All 8 present. |
| Custom agents (8) | `.github/agents/*.agent.md` | 7 project + `expert-nextjs-developer` (awesome-copilot). |
| Project skills (5) + security-review | `.github/skills/*/SKILL.md` | 5 project + `security-review/` (awesome-copilot). |
| Git pre-commit + Copilot hooks | `.githooks/pre-commit`, `.githooks/hooks/*.js`, `.github/hooks/hooks.json` | 4 hook scripts + JSON config. |
| CI workflow | `.github/workflows/ci.yml` | Lint → backend (Linux+Windows) → frontend → sidecar → e2e. |
| VS Code dev UX | `.vscode/{tasks,launch}.json` | 12 tasks, F5 launch configs. |
| Dev scripts | `scripts/dev.sh`, `scripts/dev.ps1` | One-command start, idempotent, runs MailHog + sidecar + AppHost + Next.js. |
| Hygiene | `.gitignore`, `.env.example`, `README.md` | Final. |

### 0.2 Missing or out-of-sync ❌

| # | Issue | Where | Fix |
|---|---|---|---|
| 1 | `web/`, `backend/`, `services/yfinance/`, `deploy/` folders don't exist | repo root | Wave 1. |
| 2 | `backend/tools/RbacAudit/` not scaffolded but `ci.yml` and `rbac:audit` task reference it | `backend/tools/` | Wave 1 (per your call). |
| 3 | `dev: reset-db` task uses `rm -f data/sonrisa.db && dotnet ef database update` without going through the AppHost — bypasses service discovery | `.vscode/tasks.json` | Wave 1: rewrite to use the same flow as `dev.sh`. |
| 4 | The `reset` flag in `dev.sh` / `dev.ps1` is declared but never actually deletes `data/sonrisa.db` | `scripts/dev.{sh,ps1}` | Wave 1: when `--reset`/`-Reset` is passed, do `rm -f data/sonrisa.db` before migrations. |
| 5 | `dev: tail-logs` task points at `backend/src/SonrisaNews.Api/logs/*.log` and `...Worker/logs/*.log` — these paths only exist if Serilog file sinks are configured. We plan to log to stdout/console (per `2-stack.md` §3) | `.vscode/tasks.json` | Wave 1: rewrite to tail `/tmp/apphost.log` (and the `.err` file on Windows). |
| 6 | `ci.yml` e2e job installs MailHog via `go install github.com/mailhog/MailHog@latest` — slow (compiles Go) and may fail behind GFW or in restricted networks | `.github/workflows/ci.yml` | Wave 1: download the prebuilt binary from the GitHub release instead. |
| 7 | `ci.yml` `openapi: regenerate-client` runs `pnpm generate:api` — that script doesn't exist in `web/package.json` yet | `web/package.json` | Wave 2: add the `generate:api` script as part of scaffolding. |
| 8 | `rbac_policy.csv` not created — RBAC is "the security boundary" but the file is the only artifact we'll be checking against | `backend/src/SonrisaNews.Infrastructure/Auth/` | **Superseded 2026-06-05.** RBAC is now DB-driven (5 tables: `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions` — see [`.github/instructions/rbac-policies.instructions.md`](../../.github/instructions/rbac-policies.instructions.md)). The CSV is gone; the seed migration creates the catalog. |
| 9 | `add-a-channel`, `add-a-data-source`, `add-a-matcher`, `rbac-audit`, `seed-admin-user` skills reference paths and snippets that don't exist yet (e.g. `RbacPolicyHandler.cs`, `EmailChannel.cs`) | `.github/skills/*/SKILL.md` | These are **future** guides; the templates get filled in as the first implementations land (channels in wave 4, RBAC in wave 3). The `rbac-audit` skill reads the DB (not the CSV) per the 2026-06-05 user rule. No change now. |
| 10 | `LICENSE` referenced by `README.md` doesn't exist | repo root | Wave 11: add MIT license before the first release. **Not in 24h critical path** — bump to wave 11. |
| 11 | AI onboarding path: doc says rule-based, your call was "admin configures provider, users see option only if configured" | `2-stack.md` §6.3 | Wave 8: design the `IAiProvider` interface + admin settings screen + conditional UI on the wizard. |
| 12 | AppHost, Api, Worker are separate projects — your call was "easily changeable to microservices" | `2-stack.md` §3 | Confirmed. No change to architecture; the AppHost + Aspire path **is** the microservice-ready shape. |

### 0.3 Pre-flight check (do this before wave 1)

```
[ ] .NET 10 SDK installed (dotnet --version → 10.0.x)
[ ] Aspire workload installed (dotnet workload list → contains 'aspire')
[ ] Node 22 + pnpm installed (node -v, pnpm -v)
[ ] Python 3.12 + uv installed (python --version, uv --version)
[ ] MailHog installed (mailhog -version, OR MailHog.exe in PATH)
[ ] Git hooks installed (git config core.hooksPath → .githooks)
[ ] jq installed (needed by .githooks/hooks/*.js; npm i -g jq on Windows)
```

Run `./scripts/dev.sh` once now — it should fail with a clear "missing project" error from `dotnet ef`, not crash. If it crashes in an unexpected way, fix the script before wave 1.

---

## 1. Build order (the 12 waves)

Each wave is sized for **1–2 hours** of focused work. The first three are scaffold + plumbing; waves 4–10 are the product; waves 11–12 are polish + release.

| # | Wave | Goal | First green test |
|---|---|---|---|
| 1 | **Scaffold + plumbing** | Solution builds, AppHost boots, "Hello world" round trip | `dotnet build` passes, `pnpm build` passes, AppHost dashboard reachable at `:15000` | ✅ (2026-06-04) |
| 2 | **Database + persistence skeleton** | EF Core + SQLite + first migration applies; `dotnet ef` is wired | `dotnet ef database update` against `:memory:` succeeds, integration test reads/writes a row | ✅ (2026-06-04) |
| 3 | **Auth + RBAC** | Sign-up, sign-in, refresh, `[Authorize]` works, RBAC is DB-driven (5 tables, seeded), audit tool runs | xUnit: `SignUp_DuplicateEmail_Returns409`; rbac-audit tool exits 0 | ✅ (2026-06-05) |
| 4 | **Channel abstraction** | `INotificationChannel`, `EmailChannel` (via MailHog), `SlackChannel`, channel verify flow | xUnit: `EmailChannel_SendAsync_HitsSmtpServer` (with a fake `SmtpClient`); contract test for both | ✅ (2026-06-05) |
| 5 | **Alert CRUD + filters** | Alert entity, filters JSON per type, CRUD endpoints, channel-mode matrix | xUnit: `CreateAlert_NewsWithKeywordFilter_PersistsFilter`; Playwright: create an alert in the UI | ✅ (2026-06-05, post-pause) |
| 6 | **News poller + matcher** | RSS `IDataSource`, matcher engine, `Match` audit row, dispatcher wired | xUnit: `Matcher_NewsAlertWithKeywordFilter_MatchesWhenTitleContains` (red → green) | ✅ (2026-06-05) |
| 7 | **yfinance sidecar + market poller** | FastAPI `GET /quote` + `/quotes`, `YfinanceClient`, `MarketPoller`, market matcher | pytest: `test_quote_returns_expected_shape`; xUnit: `MarketMatcher_PercentChangeInWindow_TriggersAlert` |
| 8 | **Disaster poller + dispatcher + digests** | USGS/GDACS/NHC sources, dispatcher (realtime + digests), quiet hours | xUnit: `Dispatcher_QuietHours_DefersSendUntilNextWindow`; `DigestScheduler_15mMode_BatchesMatches` |
| 9 | **Onboarding wizard (3 paths) + dashboard** | 3-path wizard, AI provider admin setting, dashboard with "test alert" button | Playwright: sign-up → onboarding (each path) → land on dashboard |
| 10 | **Admin: sources, users, health, announcements** | Admin area behind `Authorize(Policy=...)`, source mgmt, user mgmt, health page, manual announce | Playwright: admin sign-in → toggle a source off → verify next poll skips it |
| 11 | **E2E suite + observability + polish** | E2E: sign-up → alert → email received (via MailHog). Error logs → admin health. README polish | Playwright: full happy-path e2e green on Linux + Windows |
| 12 | **Release prep** | LICENSE, CONTRIBUTING, deploy/ systemd + Windows Service scaffolds (skeleton only), `git tag v0.1.0-mvp` | Manual: clean `git clone` → `./scripts/dev.sh` → app boots on a fresh machine |

---

## 2. Wave details (the 12 steps)

Each step lists: **scope** (what's in), **out of scope** (what waits), **tripwires** (which harness rules apply), **verify** (the command that says "this wave is done"), and **agent** (which persona to use).

---

### Wave 1 — Scaffold + plumbing (1–2h)

**Scope**
- `web/`: Next.js 16 + React 19 + TypeScript strict + MUI v9, route groups `(marketing)`, `(auth)`, `(app)`, `(admin)`, `lib/api` with `openapi-fetch` placeholder.
- `backend/SonrisaNews.sln` with 7 projects: `Api`, `Worker`, `Domain`, `Infrastructure`, `Shared`, `AppHost`, `tools/RbacAudit`.
- `services/yfinance/`: FastAPI + yfinance + pytest scaffold.
- `deploy/`: empty `README.md` placeholder.
- Add `web/package.json` scripts: `dev`, `build`, `start`, `lint`, `test`, `test:e2e`, `generate:api`.
- Fix audit issues #3, #4, #5, #6, #7 from §0.2.

**Out of scope**
- No real routes, no controllers, no DB schema. Just "Hello world" at `/` and a stub `GET /healthz` returning 200.

**Tripwires**
- [csharp-dotnet.instructions.md](../../.github/instructions/csharp-dotnet.instructions.md) — file-scoped namespaces, primary constructors.
- [nextjs-react.instructions.md](../../.github/instructions/nextjs-react.instructions.md) — no default exports for components, `sx` over `style`.
- [python-fastapi.instructions.md](../../.github/instructions/python-fastapi.instructions.md) — type hints everywhere, injected `httpx.AsyncClient`.

**Verify**
```bash
cd backend && dotnet build
cd web && pnpm install && pnpm build
cd services/yfinance && uv sync && uv run pytest
dotnet run --project backend/src/SonrisaNews.AppHost   # boots, dashboard at :15000
curl http://localhost:5080/healthz                     # returns 200
```

**Agent**: Stack Doc Researcher (to verify Next.js 16 + MUI v9 setup commands), then TDD C# Implementer + TDD Next.js Implementer for the scaffolds.

**Status**: ✅ Shipped 2026-06-04. The full scaffold landed in `f2aa289 Backend scaffold` and `ff4d130 Frontend scaffold`. `dotnet build` clean, `pnpm build` clean (7 routes prerendered), AppHost dashboard reachable on `:15000`, `GET /healthz` returns 200. Two follow-up review passes (one UI, one backend) applied directly to the working tree as part of the wave — the 12 file-fix follow-up is documented in [`docs/handoffs/wave1-handoff.md`](../handoffs/wave1-handoff.md) under "Wave 1 — UI fixes follow-up" and "Wave 1 — Backend fixes follow-up". Open items: the `paths = Record<string, never>` placeholder in `web/lib/api/schema.ts` is replaced by the first real `pnpm generate:api` run in wave 3+; the `(app)` and `(admin)` redirect-in-layout was replaced by a real `AuthGate` in wave 3.

---

### Wave 2 — Database + persistence skeleton (1–2h)

**Scope**
- `SonrisaNewsDbContext` in `Infrastructure/Persistence/`, registered in `Program.cs` (Api) and `Program.cs` (Worker).
- Entities: `User`, `Channel`, `Alert`, `Event`, `Match`, `Notification`, `Source`, `AuditLog`, plus the join tables — **all 10 from `1-features.md` §4**, scaffolded with at least `Id` + `CreatedAt` so the migration applies.
- `IndexAttribute`s for `(source_id, occurred_at)`, `(alert_id, fired_at)`, `(user_id, sent_at desc)` per the doc.
- First migration: `dotnet ef migrations add InitialSchema`.
- `data/sonrisa.db` gitignored.

**Out of scope**
- No repositories yet. No `AddAsync`/`SaveChanges` calls. Just the schema.

**Tripwires**
- [database-migrations.instructions.md](../../.github/instructions/database-migrations.instructions.md) — forward-only, both `Up` + `Down`, commit snapshot.
- No SQLite-specific types (the prod swap to Postgres must work).
- [testing.instructions.md](../../.github/instructions/testing.instructions.md) — hermetic; tests use `:memory:`.

**Verify**
```bash
dotnet ef database update --project backend/src/SonrisaNews.Infrastructure
sqlite3 data/sonrisa.db ".tables"   # shows the 10 tables
dotnet test backend/SonrisaNews.UnitTests --filter Category=Database
```

**Agent**: TDD C# Implementer.

**Status**: ✅ Shipped 2026-06-04. The 10 entities from `1-features.md` §4 are scaffolded in [`backend/src/SonrisaNews.Domain/Entities/`](../../backend/src/SonrisaNews.Domain/Entities) with the three required composite indexes (`IX_Events_SourceId_OccurredAt`, `IX_Matches_AlertId_FiredAt`, `IX_Notifications_UserId_SentAt`). FK relationships declared in every dependent `IEntityTypeConfiguration`. `InitialSchema` migration applied to `data/sonrisa.db`. Test suite is 32 passing, 0 failing, 0 warnings. Deferred items live in [`docs/handoffs/wave2-to-future.md`](../handoffs/wave2-to-future.md) — 21 items, each with an "Action in wave N" line.

---

### Wave 3 — Auth + RBAC (2h)

**Scope**
- `User` entity fleshed out (email, password_hash, display_name, time_zone, status, must_change_password).
- `EmailVerification`, `PasswordResetToken`, `RefreshToken` tables.
- **RBAC catalog** (5 tables, per the 2026-06-05 user rule): `Roles`, `Permissions`, `UserRoles`, `RolePermissions` — seeded by an `INSERT` in the wave 3 migration. `User.Role` column is removed; the role is a row in `UserRoles`.
- `Permissions.cs` constants for **all 16 permissions** (the 15 from `rbac-policies.instructions.md` plus `Profile.Read`).
- `RbacPolicyHandler` registered in DI. The handler runs a single SQL JOIN per request (`UserRoles ⨝ RolePermissions`); no Casbin, no CSV. The result is cached per request.
- ASP.NET Core `JwtBearer` middleware + refresh cookie.
- Controllers: `AuthController` (signup, signin, refresh, signout, verify, forgot, reset) + `MeController` (`GET /me`, guarded by `Profile.Read`).
- `AuditLog` filter registered globally on `(admin)/` controllers.
- `tools/RbacAudit` console app implemented (Roslyn: parse controllers, query the DB, exit non-zero on "missing"). **Replaces the CSV-based audit.**
- `AdminSeeder` hosted service: creates the bootstrap admin and inserts a `UserRoles` row.
- Frontend: `(auth)/signin`, `(auth)/signup`, `(auth)/verify`, `lib/auth` with refresh interceptor.

**Out of scope**
- No onboarding wizard (wave 9). After signup, user lands on a stub "welcome" page.
- No password reset email **template** (use a basic text email for now; React Email lands in wave 9).

**Tripwires**
- [rbac-policies.instructions.md](../../.github/instructions/rbac-policies.instructions.md) — every change to `Auth/` or the role/permission tables ships with a positive AND a negative test. Both mandatory. Validation is by permission, never by role — `[Authorize(Roles = ...)]` and `if (user.Role == ...)` are forbidden.
- [secrets.instructions.md](../../.github/instructions/secrets.instructions.md) — no real `Jwt__SigningKey` in code, env-var only.
- [openapi-schema.instructions.md](../../.github/instructions/openapi-schema.instructions.md) — `[ProducesResponseType]` for success + 4xx.
- Use the [seed-admin-user](../../.github/skills/seed-admin-user/SKILL.md) skill.
- Use the [rbac-audit](../../.github/skills/rbac-audit/SKILL.md) skill before opening the PR. The tool reads the DB, not a CSV.

**Verify**
```bash
dotnet test backend/SonrisaNews.UnitTests --filter Category=Auth
dotnet test backend/SonrisaNews.IntegrationTests --filter Category=Auth
dotnet run --project backend/tools/RbacAudit
# 0 "missing" findings, exit 0
curl -X POST http://localhost:5080/auth/signup -d '{"email":"a@b.com","password":"..."}' -i   # 201
curl -X POST http://localhost:5080/auth/signin -d '{"email":"a@b.com","password":"..."}' -i   # 200, refresh cookie set
curl http://localhost:5080/alerts  # 401
curl http://localhost:5080/admin/users -H "Authorization: Bearer <user-token>"  # 403
curl http://localhost:5080/admin/users -H "Authorization: Bearer <admin-token>"  # 200
```

**Agent**: TDD C# Implementer (auth) + TDD Next.js Implementer (signin/signup) + Backend Reviewer (PR-time).

**Status**: ✅ Shipped 2026-06-05. The wave 3 backend landed in `ffdcab5 Auth + RBAC backend` and the frontend in `526f6e5 auth frontend` (+ `2c577de signIn front test`, `e50eaf2 wave 3 frontend handoff`). RBAC is **DB-driven** (5 tables: `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`) — the Casbin CSV is gone. 16 permission constants in `backend/src/SonrisaNews.Domain/Auth/Permissions.cs`; 26 role↔permission grants seeded by `20260605051544_AddRbacCatalog`. The `User.Role` column was removed; role is now a row in `UserRoles`. User rule 2026-06-05 ("validation is by permission, never by role") is enforced in three layers (no `Role` claim in the JWT, no `[Authorize(Roles = ...)]` allowed, handler answers only the coarse "may a user with these roles perform this action" question). 45 tests pass; the `RbacAudit` tool is real (reads the DB, not a CSV) and exits 0 with 15 expected orphan permissions (all future-wave). Full detail in [`docs/handoffs/wave3-handoff.md`](../handoffs/wave3-handoff.md) and [`docs/handoffs/wave3-frontend-handoff.md`](../handoffs/wave3-frontend-handoff.md). The `EmailVerification.Token` and `PasswordResetToken.Token` plaintext storage concern raised in `wave2-to-future.md` §2 is **still open** — the wave 3 auth controller writes plaintext, the wave 3.1+ (or wave 11 polish) PR must hash before insert (Argon2id or HMAC-SHA256, matching the `RefreshToken.TokenHash` pattern).

---

### Wave 4 — Channel abstraction (1–2h)

**Scope**
- `INotificationChannel` interface exactly as in `2-stack.md` §6.1.
- `EmailChannel`: MailKit SMTP. Verification = send a 6-digit code to the address; `VerifyAsync(destination, code)` checks the code.
- `SlackChannel`: HttpClient → POST to incoming webhook. Verification = send a "Sonrisa News verification: …" message.
- `Notification` + `AlertChannelMode` entities fleshed out.
- `ChannelsController` with `POST /channels`, `POST /channels/{id}/verify`, `DELETE /channels/{id}`.
- Frontend: `features/channels` module + a "Add channel" dialog on the dashboard.

**Out of scope**
- Quiet hours, digests (wave 8).

**Tripwires**
- Use the [add-a-channel](../../.github/skills/add-a-channel/SKILL.md) skill — it has the checklist.
- DI: `AddKeyedScoped<INotificationChannel, EmailChannel>("email")`. **No `switch` in the dispatcher** — resolve via key.
- Verification is mandatory before any real alert is sent to a channel.
- [testing.instructions.md](../../.github/instructions/testing.instructions.md) — fake `SmtpClient`, fake `HttpMessageHandler`. No live SMTP/Slack in unit tests.
- [openapi-schema.instructions.md](../../.github/instructions/openapi-schema.instructions.md) — all 4xx documented.

**Verify**
```bash
dotnet test backend/SonrisaNews.UnitTests --filter Category=Channels
#  EmailChannel_SendAsync_HitsSmtpServer (with a fake)
#  EmailChannel_VerifyAsync_WrongCode_ReturnsFalse
#  SlackChannel_SendAsync_PostsToWebhook (with a fake handler)
#  ChannelDispatcher_UnknownType_Throws
```

**Agent**: TDD C# Implementer + TDD Next.js Implementer (Add Channel dialog).

**Status**: ✅ Shipped 2026-06-05. Backend landed in `866b16b channel backend`; frontend landed in `9a015d2 channel frontend`. 66 tests pass (0 failed, 0 skipped) — 18 added in this wave across `EmailChannelTests` (11), `SlackChannelTests` (7), and `ChannelDiRegistrationTests` (3). The wave shipped the **full** scope from `add-a-channel`: both channels implement the `INotificationChannel` interface end-to-end (`Type`, `StartVerificationAsync`, `VerifyAsync`, `SendAsync`), are registered in DI via `AddSonrisaNewsHttpClient()` (fixes a latent DI crash — `EmailChannel` and `SlackChannel` take `IHttpClientFactory` which was never registered), and have unit tests for happy path + verification failure + expiry. Verification codes expire after 24h via the new `VerificationChallenge.ExpiresAt` + `ChannelDefaults.VerificationTtl`. `Notification.DedupeKey` (Guid) + unique composite index `UX_Notifications_ChannelId_DedupeKey` added via `20260605074133_AddNotificationDedupeKey` migration (Q8 from §4 answered). Frontend `AddChannelDialog` shipped with a 4-step UX (type → destination → challenge code → confirmation). Deferred items are tracked in [`docs/handoffs/wave4-handoff.md`](../handoffs/wave4-handoff.md) §2.1–§2.5 and [`docs/handoffs/wave4-frontend-followup.md`](../handoffs/wave4-frontend-followup.md); the two that are still "open as a code change" are: (a) the `appsettings.json.new` stale file to be deleted before any open PR, and (b) the MailKit/MimeKit `NU1902` advisories to bump to 4.9.0+ in wave 8 (or a pre-wave-8 bump PR). The dispatcher-side "block `SendAsync` when `Channel.Verified = false`" contract is the wave 8 implementer's responsibility, not wave 4.

---

### Wave 5 — Alert CRUD + filters (2h)

**Scope**
- `Alert` entity with `filters` JSON (typed via Zod on the frontend, validated via FluentValidation on the backend).
- `AlertChannelMode` join table, full CRUD.
- `AlertsController` with `GET/POST/PUT/DELETE /alerts`, `GET/POST /alerts/{id}/channels`.
- Filters: News (sources, keywords, tags), Market (symbols, threshold, window), Disaster (regions, event types, severity).
- Frontend: alert editor in `features/alerts`, with the channel-mode matrix (rows = alerts, columns = channels, cell = mode dropdown).
- "Test this alert" button: stub now, wired in wave 6 when the matcher exists.

**Out of scope**
- Pollers, matcher, dispatcher. Alerts are inert until wave 6.

**Tripwires**
- [rbac-policies.instructions.md](../../.github/instructions/rbac-policies.instructions.md) — `Alerts.Write.Own` is enforced. A user can only CRUD their own alerts.
- Filters JSON is schema-validated per type; unknown fields are rejected.
- [openapi-schema.instructions.md](../../.github/instructions/openapi-schema.instructions.md) — `AlertsDto` is the OpenAPI contract; frontend codegen uses it.
- [csharp-dotnet.instructions.md](../../.github/instructions/csharp-dotnet.instructions.md) — DTOs are `record`s.

**Verify**
```bash
dotnet test backend/SonrisaNews.UnitTests --filter Category=Alerts
#  CreateAlert_NewsWithKeywordFilter_PersistsFilter
#  CreateAlert_InvalidFilter_Returns400
#  UpdateAlert_AsOtherUser_Returns403
pnpm --dir web test --filter alerts
#  Renders the channel-mode matrix
#  Add Channel dialog opens, closes on Esc
```

**Agent**: TDD C# Implementer + TDD Next.js Implementer.

**Status**: ✅ Shipped 2026-06-05 (post-pause — see timeline note at the top of this doc). Backend landed in commit `778076f`; frontend landed in `090a48b`. The original scope above is the contract; the handoffs [`docs/handoffs/wave5-handoff.md`](../handoffs/wave5-handoff.md) and [`docs/handoffs/wave5-frontend-handoff.md`](../handoffs/wave5-frontend-handoff.md) are the source of truth for what actually landed, what was deferred, and what's still open.

---

### Wave 6 — News poller + matcher (2h)

**Scope**
- `IDataSource` interface exactly as in `2-stack.md` §6.2.
- `RssSource`: feed URL, fetch every 2 min, normalize to `RawEvent`, dedupe by `external_id`.
- `NewsPoller` BackgroundService: every 2 min, iterate enabled `Source` rows of `Type = News`, call `FetchAsync`, insert new `Event` rows.
- `Matcher` service: after each poll, for each enabled alert of matching type, evaluate filters against the new events, insert `Match` rows.
- "Test this alert" button: re-runs the matcher with the most recent 50 events for that alert and shows what *would* have fired.

**Out of scope**
- Dispatcher, digest modes, quiet hours (wave 8).
- Market, Disaster (waves 7, 8).

**Tripwires**
- Use the [add-a-data-source](../../.github/skills/add-a-data-source/SKILL.md) skill.
- Use the [add-a-matcher](../../.github/skills/add-a-matcher/SKILL.md) skill.
- [testing.instructions.md](../../.github/instructions/testing.instructions.md) — `IClock` for time, fake `HttpMessageHandler` for the RSS HTTP call, hermetic.
- Dedupe is on `(source_id, external_id)`. A flaky RSS that re-emits the same item must not create duplicate events.

**Verify**
```bash
dotnet test backend/SonrisaNews.UnitTests --filter Category=Poller
#  RssSource_FetchAsync_NormalizesItems (with a fake handler returning fixture XML)
#  RssSource_DuplicateExternalId_IsIdempotent
#  Matcher_NewsAlertWithKeywordFilter_MatchesWhenTitleContains
#  Matcher_NewsAlertWithAndKeywords_RequiresAll
#  Matcher_NewsAlertWithOrKeywords_RequiresAny
dotnet test --filter Category=WorkerIntegration
#  NewsPoller_RunsOnSchedule_PersistsEvents
pnpm --dir web test:e2e --grep "test alert"
#  Click "Test alert" → see would-have-fired list
```

**Agent**: TDD C# Implementer (heavy backend) + TDD Next.js Implementer (test button UI) + Backend Reviewer (PR-time).

**Status**: ✅ Shipped 2026-06-05 (post-pause — wave 5's "picked up and shipped afterwards" window continued with this wave). Backend landed via the wave-6 PR; frontend landed via the wave-6 frontend PR (commit hashes not reported in the handoffs). Build clean (`dotnet build` → 0 errors, 0 new warnings; pre-existing `NU1902` MailKit/MimeKit advisories unchanged from wave 4, deferred to wave 8 per the wave-4 handoff). Backend tests: **155 passed, 0 failed, 0 skipped** (+45 from wave 5's 110). Frontend tests: **85 passed, 0 failed, 0 skipped** (4 revision rounds applied; the last 2 rounds were SHOULD fixes / NIT cleanups and added no new tests, with one race-fix test added in revision 2). **RBAC audit**: PASSED. 12 expected orphan permissions (all future-wave) unchanged. **Migration**: `20260605161001_AddAlertsTestOwnPermission` — adds the `Alerts.Test.Own` permission row + grants it to `User` + `Admin` (forward-only; `Up` + `Down` both implemented). The wave shipped the **full** mvp-checklist scope: `IDataSource` + `RawEvent` (Domain), `RssSource` (RSS + Atom-fallback) + `EventIngestService` (dedupe by `(SourceId, ExternalId)`) + `ISourceRegistry` (DB-driven registry), `INewsMatcher` + `NewsMatcher` (type-aware, pure-predicate + idempotent insert), `NewsPoller` + `NewsPollerRunner` (2-min tick, throws caught + logged), and the `POST /api/v1/alerts/{id}/test` "Test this alert" endpoint guarded by the new `Alerts.Test.Own` permission. **Latent DI bug found and fixed**: `NewsPollerRunner` was being `GetRequiredService`d by the hosted service but never registered with DI; the production worker would have crashed on first tick (R6). Post-wave review fixes R1–R7 all applied, plus a doc-tightening pass (D1–D6) and a host-startup smoke test that catches future DI regressions at the test stage. Deferred items: see [`docs/handoffs/wave6-handoff.md`](../handoffs/wave6-handoff.md) §5 (sources seed, admin "Fetch now" button, per-source `PollInterval` enforcement, `Matcher.Run` orphan permission) and [`docs/handoffs/wave6-frontend-handoff.md`](../handoffs/wave6-frontend-handoff.md) §1 (23 NIT items deferred to wave-11 polish).

---

### Wave 7 — yfinance sidecar + market poller (2h)

**Scope**
- `services/yfinance/`: FastAPI service with `GET /quote?symbol=AAPL` and `GET /quotes?symbols=AAPL,MSFT`. In-process TTL 60s cache. yfinance behind a thin abstraction so we can swap to a paid provider later.
- `YfinanceClient` (C#): typed `HttpClient` with `Microsoft.Extensions.Http.Resilience` (Polly v8) retries.
- `MarketPoller` BackgroundService: every 5 min, for each enabled market `Source`, call `/quotes`, store latest quote.
- Market matcher: rolling-window % change. For each market alert, evaluate against the new quote; trigger if `|change_pct| >= threshold` within the window.

**Out of scope**
- Real-time prices (delayed by 15 min per `1-features.md` §2.3.2 — document this in the UI).

**Tripwires**
- [python-fastapi.instructions.md](../../.github/instructions/python-fastapi.instructions.md) — type hints, `async/await` only, injected `httpx.AsyncClient`, structured logging.
- [testing.instructions.md](../../.github/instructions/testing.instructions.md) — yfinance mocked at the import boundary; `respx` for httpx.
- Sidecar down = worker logs warning, skips this round, health page flags it. **Never crash the worker.**
- UI label: "data delayed up to 15 minutes".

**Verify**
```bash
cd services/yfinance && uv run pytest
#  test_quote_returns_expected_shape (with a mock)
#  test_quotes_batches_correctly
#  test_cache_ttl
dotnet test backend --filter Category=Market
#  YfinanceClient_RetriesOn5xx
#  YfinanceClient_GivesUpAfterTimeout
#  MarketPoller_PersistsLatestQuote
#  MarketMatcher_PercentChangeInWindow_TriggersAlert
#  MarketMatcher_BelowThreshold_DoesNotTrigger
```

**Agent**: TDD C# Implementer + (no frontend work; price change is silent until dispatched in wave 8).

---

### Wave 8 — Disaster poller + dispatcher + digests (2h)

**Scope**
- `UsgsEarthquakeSource`, `GdacsSource`, `NhcHurricaneSource` — three `IDataSource` impls.
- `DisasterPoller` BackgroundService: every 5 min, fetch + dedupe + insert.
- Disaster matcher: region filter (country / US state / global), event type filter, severity threshold.
- `Dispatcher` BackgroundService: reads `Match` rows, routes to channels per `AlertChannelMode`.
  - `realtime` — send immediately, **except** when the channel's quiet hours are active → queue and re-attempt at the next allowed moment.
  - `digest-15m` / `digest-hourly` — wait for the next tick.
  - `digest-daily` — wait until the user's chosen hour (default 08:00 local).
- `DigestScheduler` BackgroundService: per (user, mode) tick, builds the digest, sends.
- Channel quiet hours: `Channel.QuietHoursStart` / `QuietHoursEnd` (nullable, local time). UI control on the channel settings.
- `Cleanup` BackgroundService: prune `Event` > 30d, `Notification` > 30d, `AuditLog` > 90d, expired tokens every hour. (Per `1-features.md` resolved decision 5.)

**Out of scope**
- Admin source-management UI (wave 10) — for now sources are seeded by migrations.
- Onboarding wizard (wave 9).

**Tripwires**
- [rbac-policies.instructions.md](../../.github/instructions/rbac-policies.instructions.md) — `Matcher.Run` is granted to `System` only (via `RolePermissions`); the dispatcher is invoked by the worker, not by a controller.
- Reliability: `Match (alert_id, event_id)` is unique. Dispatcher is idempotent: re-runs after a crash must not double-send.
- Quiet hours: the queued notification must surface on the admin health page if it's still queued > 24h.
- The `INotificationChannel` interface is the seam. **No `if (channel.Type == "email")` in the dispatcher** — keyed DI.

**Verify**
```bash
dotnet test backend --filter Category=Dispatcher
#  Dispatcher_RealtimeMode_SendsImmediately
#  Dispatcher_QuietHours_DefersSendUntilNextWindow
#  Dispatcher_QuietHours_DigestMode_Unaffected
#  DigestScheduler_15mMode_BatchesMatches
#  DigestScheduler_DailyMode_WaitsForChosenHour
#  Cleanup_OldEvents_Prunes
#  Cleanup_RetainsRecentNotifications
dotnet test backend --filter Category=Disaster
#  DisasterMatcher_RegionFilter_MatchesByCountry
#  DisasterMatcher_SeverityThreshold_FiltersBelowMin
```

**Agent**: TDD C# Implementer (heavy) + Backend Reviewer.

---

### Wave 9 — Onboarding wizard + dashboard (2h)

**Scope**
- Three-path wizard at `(app)/onboarding/`:
  - **Path A (AI-assisted)**: 5-question chat-style form, sends to `IAiProvider` (per your call: provider is admin-configured; if not configured, this option is hidden in the UI for the user).
  - **Path B (manual)**: alert builder with empty state.
  - **Path C (skip)**: 3 default alerts (one of each category), straight to dashboard.
- `IAiProvider` interface + `RuleBasedProvider` as the default implementation. `OpenAiProvider` as a stub (interface only — no key required, returns "not configured" until admin enables).
- Admin: `Settings → AI provider` — pick provider, set key, save. The user's wizard reads a public `GET /ai/status` to decide whether to show Path A.
- Dashboard at `(app)/alerts/`: list of alerts grouped by type, "test this alert" button (wired to wave 6 matcher), per-alert toggle, last fired, channel-mode matrix, edit, delete.
- React Email templates for: email verification, password reset, "you got a new match" notification, digest summary.

**Out of scope**
- Activity tab (defer to wave 11).
- Settings tabs (defer to wave 11).

**Tripwires**
- [nextjs-react.instructions.md](../../.github/instructions/nextjs-react.instructions.md) — Server Components by default; the wizard is a client component.
- i18n keys: even with only `en.json`, all strings go through `t('…')`. Adding a language is a file, not a code change.
- AI provider is admin-only configuration. Users never see a key field.
- [secrets.instructions.md](../../.github/instructions/secrets.instructions.md) — OpenAI key via env var or admin form, **never** in the frontend.

**Verify**
```bash
pnpm --dir web test:e2e --grep "onboarding"
#  Path A: AI option hidden when no provider configured
#  Path A: AI option visible and produces suggestions when configured
#  Path B: lands on alert builder
#  Path C: lands on dashboard with 3 default alerts
#  Dashboard: shows alerts grouped by type
#  Test alert button: shows would-have-fired list
#  React Email: render template (snapshot test) — matches latest snapshot
```

**Agent**: TDD Next.js Implementer + TDD C# Implementer (AI status endpoint) + Frontend Reviewer.

---

### Wave 10 — Admin (2h)

**Scope**
- `(admin)/layout.tsx` — guards on `role == Admin`.
- `(admin)/sources/`: list, enable/disable, add/edit (News, MarketSymbol, Disaster types), "fetch now" button, last fetch time, last error.
- `(admin)/users/`: search, view, change role, suspend, restore, hard delete. 7-day soft-delete window.
- `(admin)/health/`: source status grid, delivery success rate, matcher queue depth, top 20 errors (last 24h), oldest unprocessed event age.
- `(admin)/announcements/`: compose, target (all / segment / specific users), choose channels, daily rate limit (default 1/week per user).
- All admin actions write to `AuditLog` via the global filter (wave 3 set it up; verify here).
- "AI provider" admin setting on a `(admin)/settings/` tab.

**Out of scope**
- OTel / Grafana (roadmap). Free in MVP = stdout + SQLite counters.

**Tripwires**
- [rbac-policies.instructions.md](../../.github/instructions/rbac-policies.instructions.md) — every admin endpoint has `[Authorize(Policy = "...")]`. Run the rbac-audit tool before each PR.
- [security-review](../../.github/skills/security-review/SKILL.md) skill on this wave.
- [openapi-schema.instructions.md](../../.github/instructions/openapi-schema.instructions.md) — admin DTOs are versioned separately in OpenAPI? No — same doc. The frontend codegen treats them as tagged endpoints.

**Verify**
```bash
dotnet test backend --filter Category=Admin
#  SourcesController_Put_AsUser_Returns403
#  UsersController_Suspend_AsAdmin_AuditsAction
#  HealthController_ReturnsAggregatedCounts
pnpm --dir web test:e2e --grep "admin"
#  Non-admin gets redirected from /admin
#  Admin can disable a source; next poll skips it
#  Admin can suspend a user; user can't sign in
dotnet run --project backend/tools/RbacAudit
#  0 missing, 0 unused warnings
```

**Agent**: TDD C# Implementer + TDD Next.js Implementer + Backend Reviewer + Frontend Reviewer.

---

### Wave 11 — E2E + observability + polish (1–2h)

**Scope**
- E2E: `web/e2e/auth.spec.ts`, `web/e2e/alert-happy-path.spec.ts`, `web/e2e/admin.spec.ts`.
  - Happy path: sign-up → onboarding (path C) → create alert → wait for poll → assert email in MailHog.
  - Admin path: admin sign-in → toggle a source off → verify next poll skips it.
- Activity tab on dashboard: feed of last 30 days of notifications, resend link, mark as read, view source event.
- Settings tabs: account, channels, notification preferences (default delivery mode per type, quiet hours, daily digest hour).
- Status page at `/status` (read-only): `/api/health` + last-poll timestamps per source.
- README polish with screenshots/GIFs of the dashboard.
- Make the `dev: tail-logs` task actually work (rewrite per audit #5).

**Out of scope**
- Lighthouse/performance budget enforcement (roadmap).
- Real OTel export (roadmap).

**Tripwires**
- [testing.instructions.md](../../.github/instructions/testing.instructions.md) — E2E uses Playwright, no mocking. MailHog for email assertions.
- Stable selectors (`getByRole` first). No `waitForTimeout`.
- The E2E job is Linux-only in MVP. Windows is a manual check on wave 12.

**Verify**
```bash
pnpm --dir web test:e2e
#  3 spec files, all green
curl http://localhost:3000/status
#  200, shows last poll times per source
```

**Agent**: TDD Next.js Implementer + TDD C# Implementer (status endpoint) + Frontend Reviewer.

---

### Wave 12 — Release prep (30–60 min)

**Scope**
- `LICENSE` (MIT) at repo root.
- `CONTRIBUTING.md` with the harness rules.
- `deploy/systemd/sonrisa.service` and `deploy/windows-service/install.ps1` — skeleton only, with comments. Not wired.
- `CHANGELOG.md` with v0.1.0-mvp entry.
- `git tag v0.1.0-mvp`.
- Manual: clean `git clone` on a second machine, `./scripts/dev.sh` → app boots end-to-end.

**Out of scope**
- A real deploy. This is a milestone tag, not a release.

**Tripwires**
- [roadmap-sync](../../.github/agents/roadmap-sync.agent.md) agent opens a PR to flip the status in `1-features.md`. You merge.

**Verify**
```bash
# clean machine
git clone ...
cd sonrisa && ./scripts/dev.sh
# app at :3000, can sign up, can create an alert
git tag v0.1.0-mvp
```

**Agent**: Roadmap Sync (PR only — you merge).

---

## 3. Cross-cutting tripwires (apply to every wave)

These are the rules the agent **must** follow on every PR, in every wave. They've been lifted from `AGENTS.md`, the per-domain instructions, and the skills; this is just the consolidated view.

1. **No secrets in code.** `.env` is gitignored. Production secrets come from env vars. Use `dotnet user-secrets` for local dev.
2. **Migrations are CLI-owned.** `dotnet ef migrations add <Name>`. Never hand-edit. Both `Up` and `Down` mandatory Validation is by permission, never by role — see [`.github/instructions/rbac-policies.instructions.md`](../../.github/instructions/rbac-policies.instructions.md).. Commit the snapshot.
3. **RBAC changes ship with two tests** (one positive, one negative). `rbac-audit` exits 0 before the PR is opened.
4. **Every controller declares `[ProducesResponseType]` for success + 4xx.** The OpenAPI doc is the contract.
5. **Frontend regenerates the API client on every OpenAPI change** (`pnpm generate:api`). Drift is a bug.
6. **Tests must pass in CI before merge.** No `it.skip`, no `Fact(Skip = "…")`. If a test is broken, fix it.
7. **No `async void` in C#, no `await` without `CancellationToken` in long-running paths, no blocking I/O in async methods.**
8. **All user-visible strings go through i18n keys** (even with only `en.json` in MVP).
9. **No third-party scripts on public pages.** No tracking. Self-hosted analytics only, opt-in.
10. **PR body must include** "What changed" (1–3 bullets), "How verified" (commands run / test names), "Rollback plan" (revert commit or follow-up).
11. **One concern per PR.** Small enough to review in 5 minutes.
12. **No force-pushes to `main`.** Branch protection is on.

---

## 4. Open questions (need your call before the relevant wave)

These are decisions that affect more than one wave. If you can answer them up front, the agent doesn't have to stop mid-build.

| # | Question | Affects | Default if no answer |
|---|---|---|---|
| Q1 | For the **first** sign-in flow, do we require email verification before the user can do anything, or let them explore a read-only dashboard? | Wave 3 (auth) | Verify before exploring. (Standard pattern.) |
| Q2 | For **disaster severity thresholds**, do we hardcode defaults (`M5.0+`, `Cat 3+`) in code, or seed from a JSON file the admin can edit? | Wave 8 (disaster) | Seed from a JSON file at `data/default-thresholds.json`. |
| Q3 | The `INotificationChannel.Verification` flow: do we expire verification codes (e.g. 24h) and what happens after expiry? | Wave 4 (channels) | 24h expiry. Re-add the channel to get a new code. |
| Q4 | For the **digest-daily** mode, when the user hasn't picked an hour, do we default to 08:00 local or 09:00? | Wave 8 (dispatcher) | 08:00 local (matches the doc's example). |
| Q5 | Do we need **multi-channel mode per alert** (a single alert could send realtime to email and daily digest to Slack)? | Wave 5 (alerts) | Yes, that's already in the channel-mode matrix design. Just confirming. |
| Q6 | The **onboarding wizard** — when the user picks path A and the AI returns zero suggestions, do we silently fall back to path C defaults, or show "no matches — try manual"? | Wave 9 (wizard) | Show "no matches — try manual". |
| Q7 | For the **`dotnet ef migrations add` hook**: should it block if the migration touches a Postgres-only type? (We promised no SQLite-specific types, so this is a guardrail.) | Wave 2 onward | Yes, block. The hook lists forbidden types (`jsonb`, `tsvector`, `uuid` without a value converter, `citext`, `inet`). |
| Q8 | For the **`INotificationChannel` interface** — do we want `SendAsync` to be idempotent (safe to retry the same `NotificationPayload.Id`), or do we accept the risk of duplicates on retry? | Wave 4 (channels) | Idempotent. The `Notification` entity has a `dedupe_key` (default = `Match.Id`); the dispatcher checks before sending. |

---

## 5. Out of MVP (intentionally deferred)

Captured here so we don't forget, and so the design accounts for them. **Not in scope for the 24h build.**

- Hybrid data sources (user requests → admin approves).
- More market trigger types (price threshold, volume spike).
- More channels (SMS via Twilio, push, Discord, Teams, generic webhook).
- API tokens + read-only public API.
- Multi-language UI.
- Native mobile apps.
- Federation: shared alert templates.
- E2E encryption of source payloads at rest.
- OTel exporter → Grafana Cloud.
- Microservice split (AppHost → Compose / Kubernetes). Architecture is ready; the publisher call is a post-MVP step.

---

## 6. How to use this doc

- **You**: open this when a wave starts. Read the "Scope" and "Tripwires". Confirm or push back.
- **TDD Implementer agent**: read this when invoked. The "Verify" block is your definition of done.
- **Reviewer agent**: read this when reviewing a PR. The "Tripwires" block is your checklist.
- **Triage agent**: when something breaks, jump to the relevant wave and look at "Verify".

When a wave is done, the agent:
1. Updates the "Status" table in `README.md` (1 line per wave, manual or via Roadmap Sync).
2. Does **not** update this doc. The Roadmap Sync agent opens a PR to flip the wave from "⬜" to "✅", and you merge.
