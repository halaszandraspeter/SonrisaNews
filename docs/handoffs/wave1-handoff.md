# Sonrisa News — Wave 1 (skeleton + plumbing) handoff

Date: 2026-06-04

## What landed

- `backend/` — .NET 10 solution with 7 projects (Api, Worker, Domain, Infrastructure, Shared, AppHost, tools/RbacAudit) + 1 xUnit test project. Aspire 13 orchestrates Api+Worker; dashboard pinned to :15000, API on :5080.
- `web/` — Next.js 16.2.7 + React 19.2 + TypeScript strict + MUI v9 + React Query v5 + Zod. Marketing page at `/`, route groups for `(marketing)`, `(auth)`, `(app)`, `(admin)` all scaffolded with placeholders. OpenAPI codegen script wired.
- `services/yfinance/` — FastAPI sidecar with `/health`, `/ready`, `/quote`, `/quotes`. Deterministic hash-based fake data (no live yfinance in tests). 12 pytest tests pass.
- `deploy/` — README placeholder for post-MVP systemd/Windows Service.

## Audit fixes applied (#3, #4, #5, #6, #7)

- #3 — `dev: reset-db` VS Code task now invokes the dev script with `-Reset`/`--reset`.
- #4 — `--reset` flag in `dev.sh` / `dev.ps1` actually deletes `data/sonrisa.db` before migrations.
- #5 — `dev: tail-logs` tails `/tmp/apphost.log`, `/tmp/mailhog.log`, `/tmp/yfinance.log`, `/tmp/nextdev.log`.
- #6 — CI workflow now downloads the prebuilt MailHog binary from GitHub releases (no `go install`).
- #7 — `web/package.json` has `generate:api` script ✓.

## Verify

```
cd backend && dotnet build SonrisaNews.slnx && dotnet test SonrisaNews.slnx
cd web && pnpm install && pnpm build && pnpm test
cd services/yfinance && uv sync && uv run pytest
dotnet run --project backend/src/SonrisaNews.AppHost   # dashboard at :15000
```

## Known small debts

- `rbac_policy.csv` not created — wave 3.
- `EF migrations` not added — wave 2 (DbContext is empty except for `OnModelCreating` hook).
- Sidecar starts via `dev.sh`/`dev.ps1`, NOT via Aspire AppHost. Add it as `AddExecutable` in wave 7 when the C# MarketPoller needs service discovery.
- `RbacAudit` is a skeleton — full Casbin wiring lands in wave 3. **Partially addressed 2026-06-04** (see "Backend fixes follow-up" below): output is now `[SKELETON]`-prefixed so CI logs make the no-op nature obvious. The real audit itself is still wave 3.
- Worker is a heartbeat placeholder — pollers + matcher + dispatcher land in waves 6–8. **Partially addressed 2026-06-04**: heartbeat is now `LogTrace`-logged and the interval is a named constant (`SonrisaWorker.HeartbeatInterval`).
- No default `ConnectionStrings:Sonrisa` in `appsettings.json` — `dotnet ef` would crash on a fresh clone. **Resolved 2026-06-04** (see "Backend fixes follow-up" below).
- API and Worker configured Serilog independently, so log shapes would drift the moment one of them changed. **Resolved 2026-06-04** (see "Backend fixes follow-up" below).
- `HealthController` called `DateTimeOffset.UtcNow` directly instead of injecting `IClock` — would flake the first test that asserted on `CheckedAt`. **Resolved 2026-06-04** (see "Backend fixes follow-up" below).

## Local env additions (Windows machine)

User had to install for the verify step to actually pass:
- .NET 10 SDK (already present)
- Aspire workload (`dotnet workload install aspire` — though Aspire is now NuGet-only in 13.0, the workload is deprecated)
- pnpm 11.5.1 via `npm install -g pnpm`
- Python 3.12 via `winget install Python.Python.3.12`
- uv 0.11.19 via `winget install astral-sh.uv`
- MailHog 1.0.1 from GitHub release into `~/Downloads/MailHog/`, added to PATH
- dotnet-ef 10.0.8 (already present)

## CI vulnerability pinning

`System.Security.Cryptography.Xml` pinned to 10.0.6 in `SonrisaNews.Infrastructure.csproj` to clear GHSA-w3x6-4m5h-cxqf (DoS in EncryptedXml). This is a transitive dep of `Microsoft.Extensions.Http.Resilience 10.0.0` and would otherwise resolve to a vulnerable version.

## Wave 1 — UI fixes follow-up (2026-06-04)

Frontend Reviewer read-only review of the wave 1 `web/` skeleton. Two `CRITICAL`, six `SHOULD`, and several `NIT` findings. The six `SHOULD` items were applied directly; the two `CRITICAL` items remain open and are tracked below.

### Decisions made (defaults; both reversible)

- **CORS strategy: Next.js `rewrites()` proxy** — `next.config.ts` now proxies `/api/*` → `${env.apiUrl}/api/*`. Client is same-origin in dev and prod. No backend CORS changes needed in wave 2.
- **Route boundary policy: defer** — no `loading.tsx` / `error.tsx` / `not-found.tsx` stub files in `(app)` and `(admin)`. They ship with the first real page in waves 3 and 10. Empty placeholders would be deleted in those waves anyway ("delete unused code, Git has history").

### `SHOULD` items applied (6 files, 4 logical commits)

1. `chore(web): use @mui/material-nextjs v16-appRouter adapter`
   - `web/components/AppProviders.tsx` — `v15-appRouter` → `v16-appRouter`. Confirmed via `node_modules/@mui/material-nextjs/package.json` that v9.0.1 ships `v16-appRouter`; v15 entry predates Next 16's `cache` API surface.
2. `chore(web): proxy /api/* to backend, point client at /`
   - `web/next.config.ts` — drop the `env` block (Next exposes `process.env.NEXT_PUBLIC_*` already); add `rewrites()` proxying `/api/:path*` → `${env.apiUrl}/api/:path*`. Imported `env` from `@/lib/env`.
   - `web/lib/api/client.ts` — `baseUrl: "/"`; remove the duplicate `NEXT_PUBLIC_API_URL` fallback. Client always same-origin.
3. `chore(web): tune query retry, add <main>, aria-disabled sign-up`
   - `web/components/AppProviders.tsx` — `retry: 1` → `retry: 2` with exponential backoff (`retryDelay: attempt => min(1000 * 2^attempt, 10_000)`). `staleTime: 30_000` unchanged.
   - `web/app/(marketing)/page.tsx` — `<Container component="main">` for a real landmark; `aria-disabled` on the sign-up button; replaced `<code>:5080</code>` literal copy with a screen-reader-friendly sentence.
4. `chore(web): add typecheck script, second env test`
   - `web/package.json` — `typecheck: "tsc --noEmit"`.
   - `web/tests/env.test.ts` — added a `string + non-empty` assertion. (Cannot truly test the env-override branch without a separate test file; logged as a follow-up nit.)

### Verification

- `pnpm test` — 2/2 pass (`tests/env.test.ts`).
- `pnpm build` — succeeds, TypeScript clean (`Finished TypeScript in 2.5s`), 3/3 static pages generated.

### Open — `CRITICAL` (not fixed in this round)

- **Placeholder `paths = Record<string, never>` in `web/lib/api/schema.ts`.** `pnpm generate:api` will overwrite this on first run, but until then every `apiClient.GET(...)` is typed as `never` and silently gives back an `any` response shape. **Action for wave 2+**: add a CI check that fails if `lib/api/schema.ts` is unchanged, OR add `pnpm generate:api:check` that regenerates into a temp file and `diff`s it. Drift = a real frontend bug.
- **`(app)` and `(admin)` layouts redirect to `/` from the layout itself.** That works for the placeholder but the moment real pages land, the redirect must move into an actual auth guard and `loading.tsx` / `error.tsx` / `not-found.tsx` must ship with the first page in each group. **Action for waves 3 and 10**: when adding the first real `page.tsx` in either group, add the boundary triplet in the same PR. Reviewer will block otherwise.

### Open — `NIT` (deferred, non-blocking)

- Split `vitest.config.ts` into node + dom environments when the first component test lands. Add `pnpm add -D jsdom @testing-library/react @testing-library/jest-dom` at that point.
- Document the Playwright `webServer` collision with the Aspire AppHost `dev: up` task in the README.
- Drop `"use client"` from `web/styles/theme.ts` once confirmed build-clean (no behavioral impact, mild bundle-size win).
- Extract a pure `getApiUrl(env)` in `web/lib/env.ts` and unit-test both branches in a dedicated test file.
- Remove the wave-1 "Status: skeleton" paragraph from the marketing page in wave 11 (polish).

### Push status (read me)

The 6 changed files are in the working tree, **not committed, not on a branch, not pushed**. Per AGENTS.md §5, the agent does not push, open, or merge PRs on its own. The user opens the PR explicitly. The suggested commit shape is in the review above; four logical steps across the six files. The user applies and pushes.

### Proposed rule addendum (for user to apply in a separate PR)

A rule that "the agent never runs `git push` from a local terminal, even when the user asks" was proposed during this session. Reasoning: local `git push` bypasses the harness's `pre-tool` hooks (force-push-to-main block, file blocklist, secret scan) and is the only path on which a feature branch can land on `main` without a reviewed PR. The rule is not applied yet — it would live as Behavior hook #13 in `.github/copilot-instructions.md` and a new short section in `AGENTS.md` §5. The user applies in a labeled PR.

## Wave 1 — Backend fixes follow-up (2026-06-04)

Backend reviewer read-only review of the wave 1 `backend/` skeleton. Zero `CRITICAL`, seven `SHOULD`, two `NIT`. All `SHOULD` items were applied directly. The `NIT` items were intentionally not applied (see "Deferred" below).

### Decisions made (defaults; all reversible)

- **DB path = repo-root `data/sonrisa.db`** — matches the existing `scripts/dev.ps1:65` and the `database-migrations` instruction. Path is now centralized in `Shared/PathConstants.cs` (currently only the constants — no consumer reads from there yet, so the value is a *named placeholder*, not a wired refactor).
- **Default connection string lives in `Infrastructure/appsettings.Development.json`** as `Data Source=../../../data/sonrisa.db` (relative to the project hosting the config, i.e. `<repo>/data/sonrisa.db`). Production stays env-var-only per the `secrets.instructions.md` rule; the improved error message tells the operator what to set.
- **Serilog setup moves to `Shared/Hosting/SonrisaHostingExtensions.AddSonrisaNewsSerilog()`** — both `Api/Program.cs` and `Worker/Program.cs` call the same extension. Uses `IServiceCollection.AddSerilog(...)` (from `Serilog.Extensions.Hosting`, transitive of `Serilog.AspNetCore 8.0.3`) so the same code path works for both the Web host (`WebApplicationBuilder`) and the generic host (`HostApplicationBuilder`).

### `SHOULD` items applied (8 fixes, 1 logical commit)

The fix pass is one logical unit ("the wave-1 backend should-fix review"), so one commit is the right shape. File list:

1. **`backend/src/SonrisaNews.Infrastructure/appsettings.Development.json`** *(new)* — default `ConnectionStrings:Sonrisa` → `Data Source=../../../data/sonrisa.db`. Comment in the matching `AddSonrisaNewsDbContext` XML doc explains why `../../../` (the EF tooling runs from the repo root, not from the project dir).
2. **`backend/src/SonrisaNews.Shared/PathConstants.cs`** *(new)* — `DataDirectory = "data"`, `SonrisaDatabaseFileName = "sonrisa.db"`. Placeholder for the path; not yet consumed by `scripts/dev.ps1` (see "Deferred" below).
3. **`backend/src/SonrisaNews.Shared/Hosting/ApplicationClockServiceCollectionExtensions.cs`** *(new)* — `AddSonrisaNewsClock()` registers `IClock → SystemClock` as a singleton. The `// singleton` rationale lives with the registration, not in `Infrastructure`.
4. **`backend/src/SonrisaNews.Shared/Hosting/SonrisaHostingExtensions.cs`** *(new)* — `AddSonrisaNewsSerilog()` configures Serilog from `appsettings.json`'s `Serilog` section, enriches with `FromLogContext`, writes to console with invariant culture. XML doc on the method explains the `AddSerilog` vs `UseSerilog` choice (host-builder compatibility).
5. **`backend/src/SonrisaNews.Shared/SonrisaNews.Shared.csproj`** — added `<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />` and `<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />`. The abstractions package is small and `Shared` is allowed to know about config (it already has `ConfigurationKeys`).
6. **`backend/src/SonrisaNews.Worker/SonrisaNews.Worker.csproj`** — added `Serilog.AspNetCore 8.0.3` so the `AddSonrisaNewsSerilog` call in the Worker resolves.
7. **`backend/src/SonrisaNews.Worker/Program.cs`** — replaces the previous one-liner with `builder.Logging.ClearProviders(); builder.Services.AddSonrisaNewsSerilog();` so the Worker's log shape matches the API.
8. **`backend/src/SonrisaNews.Api/Program.cs`** — same refactor: dropped the inline `UseSerilog(...)` host-builder call, replaced with `AddSonrisaNewsSerilog()` on `IServiceCollection`. Also added an XML-style comment on the dev-only `OpenApi` / `Scalar` block marking it as the prod-disable point — the rule is "do not widen `IsDevelopment()` to include Staging without flipping `OpenApi:Enabled=false` in `appsettings.Production.json`."
9. **`backend/src/SonrisaNews.Infrastructure/InfrastructureServiceCollectionExtensions.cs`** — `AddSingleton<IClock, SystemClock>()` replaced with `AddSonrisaNewsClock()`. The registration still lives in `Infrastructure` so `Shared` doesn't grow an `Infrastructure` dependency.
10. **`backend/src/SonrisaNews.Infrastructure/Persistence/SonrisaNewsDbContextRegistration.cs`** — improved the missing-connection-string error message to mention the env-var name (`ConnectionStrings__Sonrisa`), the user-secrets project (`SonrisaNews.Infrastructure`), and the appsettings file. Added an XML `<remarks>` block on the method documenting the resolution order (env var → user-secrets → `appsettings.{Environment}.json`).
11. **`backend/src/SonrisaNews.Api/Controllers/HealthController.cs`** — `HealthResponse.Alive()` no longer takes a hard-coded `DateTimeOffset.UtcNow` argument at the call site. Controller now takes `IClock` via primary constructor and passes `clock.UtcNow` to `Alive(checkedAt)`. The test that doesn't exist yet (wave 2 or 3) will assert on `CheckedAt` deterministically.
12. **`backend/src/SonrisaNews.Worker/SonrisaWorker.cs`** — extracted the 30-second heartbeat interval as a private `static readonly TimeSpan HeartbeatInterval`. Added a `LogTrace("Heartbeat tick at {Time}", clock.UtcNow)` so the Aspire dashboard has something to show. Wrapped the `Task.Delay(..., stoppingToken)` in a `try { ... } catch (OperationCanceledException) { break; }` — the `while (!stoppingToken.IsCancellationRequested)` already handles cancellation correctly, but the explicit catch is a marker for future pollers in waves 6–8 to copy.
13. **`backend/tools/RbacAudit/Program.cs`** — every output line is now prefixed with `[SKELETON]`, including the file-not-found error path. The final `Console.WriteLine` adds a `⚠ THIS TOOL IS CURRENTLY A NO-OP` warning. Wave 3 removes the prefix in the same commit that wires the real audit.

### Verification

- `dotnet test backend/SonrisaNews.slnx` — **5 passed, 0 failed, 0 skipped, 54ms**.
- API smoke test (since torn down) confirmed `GET /healthz` and `GET /api/v1/health` both return `200`, and the controller response is `{"status":"ok","checkedAt":"2026-06-04T18:28:19.1640784+00:00"}` with the `IClock`-injected timestamp.
- Build is warning-free and error-free across all 8 projects.

### What I deliberately did *not* do (and why)

- **The AppHost `:15000` review point was a misread.** I re-checked: `dotnet run` uses the first `launchSettings.json` profile (`http`) by default regardless of who calls it, so the dashboard binds to `:15000` correctly from `scripts/dev.ps1`. No change needed. **Should have caught this in the review itself** — added to the reviewer's "verify a runtime concern with an actual repro before flagging it" checklist (see `mistakes-to-avoid.md` for the format).
- **`dotnet ef database update` failed in the smoke test with "Unable to resolve service for type `DbContextOptions<>`".** The connection string *did* resolve (we got past the explicit throw) — the failure is in EF Core's design-time host trying to construct a `DbContext` without the API's `Program.cs` DI registrations being discoverable. This is a **wave-2 problem** (`IDesignTimeDbContextFactory<SonrisaNewsDbContext>`, then the first real `dotnet ef migrations add InitialSchema`). Fixing it in wave 1 would be scope creep.
- **No production-environment connection string.** Only `appsettings.Development.json` ships one. The 12-factor-correct approach is to make prod operators set `ConnectionStrings__Sonrisa` via env vars; the improved error message guides them.
- **No user-secrets init script.** The dev connection string is in `appsettings.Development.json`. When the secrets-management tripwire becomes active in wave 3 (JWT signing key, SMTP password), the wave-3 PR adds a `dotnet user-secrets init` step to `scripts/dev.ps1` for the relevant env vars. Wave 1 has no secrets to manage.
- **`Shared/PathConstants` constants are not yet consumed by `scripts/dev.ps1`.** Adding the constants is the *first* half of the refactor (single source of truth); the *second* half is reading them in the dev script and in the Infrastructure `AddSonrisaNewsDbContext` method. Both are mechanical, but they expand the blast radius of this PR and don't ship a new behavior. **Wave-2 PR picks this up**: replace the literal `data/sonrisa.db` in `scripts/dev.{sh,ps1}` and in `appsettings.Development.json` with a small `Path.Combine(PathConstants.DataDirectory, PathConstants.SonrisaDatabaseFileName)`.

### Open — `NIT` (deferred, non-blocking)

- **Treat-warnings-as-errors** scoped to `Domain` and `Shared` (the pure-code projects). Not applied; flip when the next wave's PR has more code to compile.
- **Remove `Microsoft.AspNetCore.OpenApi 10.0.8` → Swashbuckle migration comment** in the API `Program.cs`. Not applied; comment lives naturally in the file and adds clarity. Will land when the first real controller lands in wave 3.

### Push status (read me)

The 12 changed files are in the working tree, **not committed, not on a branch, not pushed**. Per AGENTS.md §5 and the Copilot-instructions commit/PR flow, the agent does not push, open, or merge PRs on its own. The user opens the PR explicitly. The suggested commit message and PR body were in the review summary above (one logical commit, one PR for the whole should-fix pass).

### Reviewer's "verify before flag" addendum (for `mistakes-to-avoid.md`)

When the review flags a runtime concern ("the dashboard may not bind to `:15000` from `scripts/dev.ps1`"), the reviewer should reproduce the concern in a smoke test *before* writing it up. The dashboard-port claim was wrong, and the wrong claim would have driven a no-op code change. The fix is a discipline change in the reviewer's process, not a code change: a `repro: yes` / `repro: no` annotation on each runtime concern in the review, with the actual command output as evidence. Add to the `mistakes-to-avoid.md` "Behavioral" section.
